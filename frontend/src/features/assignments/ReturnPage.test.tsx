import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ReturnPage } from '@/features/assignments/ReturnPage'
import { assignedItem } from '@/features/assignments/testData'
import { Permissions } from '@/features/auth/permissions'
import { scanResult, tree } from '@/features/movements/testData'
import { FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

const manager = fakeUser({ role: 'WarehouseManager', permissions: [Permissions.CanMoveStock] })

describe('ReturnPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('takes a scanned item back as faulty, which requires a note', async () => {
    const requests: unknown[] = []
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/locations/tree': () => json(200, tree),
        'GET /api/scan/ITM-AAAAAA': () =>
          scanResult('item', 'item-1', 'ITM-AAAAAA', 'ITM-AAAAAA · SN-1'),
        'GET /api/items/item-1': () => json(200, assignedItem()),
        'POST /api/assignments/asg-1/return': async (request) => {
          requests.push(await request.json())
          return json(200, { assignmentId: 'asg-1', referenceId: 'ref-2' })
        },
      }),
    )
    const user = userEvent.setup()
    const { router } = renderRoutes(
      [
        {
          path: '/assignments/return',
          element: (
            <FakeAuth user={manager}>
              <ReturnPage />
            </FakeAuth>
          ),
        },
        { path: '/items/:id', element: <p>Adet sayfası</p> },
      ],
      '/assignments/return',
    )

    // Step 1: scan the item; its holder is shown.
    await user.type(await screen.findByLabelText('Elle giriş'), 'ITM-AAAAAA{Enter}')
    expect(await screen.findByText(/Zimmetli: Ali Saha/)).toBeInTheDocument()

    // Step 2: return location.
    await user.click(screen.getByRole('combobox', { name: /^İade lokasyonu/ }))
    await user.click(await screen.findByRole('option', { name: 'Bina › Raf B2' }))
    await user.click(screen.getByRole('button', { name: 'İleri' }))

    // Step 3: faulty needs a note before moving on.
    await user.click(await screen.findByText('Arızalı'))
    expect(screen.getByRole('button', { name: 'İleri' })).toBeDisabled()
    await user.type(screen.getByLabelText(/Arıza notu/), 'Ekran kırık')
    await user.click(screen.getByRole('button', { name: 'İleri' }))

    // Step 4: confirm.
    expect(await screen.findByText(/tamire gönderilir/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'İade al' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/items/item-1'))
    expect(requests[0]).toEqual({
      returnLocationId: 'b2',
      condition: 'Faulty',
      note: 'Ekran kırık',
    })
  })
})
