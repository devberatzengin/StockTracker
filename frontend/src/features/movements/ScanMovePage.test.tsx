import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Permissions } from '@/features/auth/permissions'
import { ScanMovePage } from '@/features/movements/ScanMovePage'
import { productHandlers, scanResult } from '@/features/movements/testData'
import { FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

const manager = fakeUser({ role: 'WarehouseManager', permissions: [Permissions.CanMoveStock] })

describe('ScanMovePage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('scans the target, an item and a quantity product, then transfers them', async () => {
    const requests: unknown[] = []
    vi.stubGlobal(
      'fetch',
      mockApi({
        ...productHandlers,
        'GET /api/scan/LOC-B20000': () =>
          scanResult('location', 'b2', 'LOC-B20000', 'Raf B2', 'Bina'),
        'GET /api/scan/ITM-AAAAAA': () =>
          scanResult('item', 'item-1', 'ITM-AAAAAA', 'ITM-AAAAAA · SN-1', 'Laptop · Bina › Raf A1'),
        'GET /api/scan/4006381333931': () =>
          scanResult('product', 'screw', 'PRD-SCREW1', 'Vida M4'),
        'POST /api/movements/transfer': async (request) => {
          requests.push(await request.json())
          return json(200, { referenceId: 'ref-9', movementCount: 2, createdItems: [] })
        },
      }),
    )
    const user = userEvent.setup()
    const { router } = renderRoutes(
      [
        {
          path: '/scan-move',
          element: (
            <FakeAuth user={manager}>
              <ScanMovePage />
            </FakeAuth>
          ),
        },
        { path: '/movements/ref/:referenceId', element: <p>Özet sayfası</p> },
      ],
      '/scan-move',
    )

    // Step 1: target shelf.
    await user.type(await screen.findByLabelText('Elle giriş'), 'LOC-B20000{Enter}')
    expect(await screen.findByText(/Sürekli mod/)).toBeInTheDocument()

    // Step 2: an item is added right away; a quantity product asks for source and quantity.
    await user.type(await screen.findByLabelText('Elle giriş'), 'ITM-AAAAAA{Enter}')
    expect(await screen.findByText('Eklendi: ITM-AAAAAA · SN-1')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Elle giriş'), '4006381333931{Enter}')
    expect(await screen.findByLabelText(/Bina › Raf A1 — 500 adet/)).toBeChecked()
    await user.type(screen.getByLabelText(/Miktar/), '200')
    await user.click(screen.getByRole('button', { name: 'Ekle' }))

    await user.click(screen.getByRole('button', { name: 'Özet (2)' }))

    // Step 3: confirm.
    await user.click(await screen.findByRole('button', { name: 'Taşı' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/movements/ref/ref-9'))
    expect(requests[0]).toEqual({
      toLocationId: 'b2',
      lines: [{ itemId: 'item-1' }, { productId: 'screw', fromLocationId: 'a1', quantity: 200 }],
    })
  })

  it('asks for the item label when a serialized product barcode is scanned', async () => {
    vi.stubGlobal(
      'fetch',
      mockApi({
        ...productHandlers,
        'GET /api/scan/LOC-B20000': () => scanResult('location', 'b2', 'LOC-B20000', 'Raf B2'),
        'GET /api/scan/1111111111116': () =>
          scanResult('product', 'laptop', 'PRD-LAPTOP', 'Laptop'),
      }),
    )
    const user = userEvent.setup()
    renderRoutes(
      [
        {
          path: '/',
          element: (
            <FakeAuth user={manager}>
              <ScanMovePage />
            </FakeAuth>
          ),
        },
      ],
      '/',
    )

    await user.type(await screen.findByLabelText('Elle giriş'), 'LOC-B20000{Enter}')
    await user.type(await screen.findByLabelText('Elle giriş'), '1111111111116{Enter}')

    expect(await screen.findByText(/adetin kendi etiketini/)).toBeInTheDocument()
  })
})
