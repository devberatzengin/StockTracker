import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AssignPage } from '@/features/assignments/AssignPage'
import { itemDetail, lookupHandler } from '@/features/assignments/testData'
import { Permissions } from '@/features/auth/permissions'
import { todayIstanbul } from '@/lib/dates'
import { FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

const manager = fakeUser({ role: 'WarehouseManager', permissions: [Permissions.CanMoveStock] })

const renderPage = (path: string) =>
  renderRoutes(
    [
      {
        path: '/assignments/new',
        element: (
          <FakeAuth user={manager}>
            <AssignPage />
          </FakeAuth>
        ),
      },
      { path: '/items/:id', element: <p>Adet sayfası</p> },
    ],
    path,
  )

describe('AssignPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('assigns a prefilled item to the chosen person with a due date', async () => {
    const requests: unknown[] = []
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/items/item-1': () => json(200, itemDetail()),
        'GET /api/users/lookup': lookupHandler,
        'POST /api/assignments': async (request) => {
          requests.push(await request.json())
          return json(200, { assignmentId: 'asg-1', referenceId: 'ref-1' })
        },
      }),
    )
    const user = userEvent.setup()
    const { router } = renderPage('/assignments/new?itemId=item-1')

    // Step 2: the item card is shown; pick the person.
    expect(await screen.findByText('Laptop · SN-1')).toBeInTheDocument()
    await user.click(screen.getByRole('combobox', { name: 'Zimmeti alacak kişi' }))
    await user.click(await screen.findByRole('option', { name: /Ali Saha/ }))
    await user.click(screen.getByRole('button', { name: 'İleri' }))

    // Step 3: due date shortcut and a note.
    await user.click(await screen.findByRole('button', { name: '1 hafta' }))
    await user.type(screen.getByLabelText('Not (opsiyonel)'), 'Saha ekibi')
    await user.click(screen.getByRole('button', { name: 'İleri' }))

    // Step 4: confirm.
    expect(await screen.findByText(/adına zimmetlenecek/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Zimmetle' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/items/item-1'))
    expect(requests[0]).toEqual({
      itemId: 'item-1',
      assigneeUserId: 'u-ali',
      dueDate: todayIstanbul(7),
      note: 'Saha ekibi',
    })
  })

  it('refuses an item that is already assigned', async () => {
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/items/item-1': () =>
          json(
            200,
            itemDetail({
              status: 'Assigned',
              availableActions: ['Return'],
              openAssignment: {
                id: 'asg-1',
                assigneeUserId: 'u-ali',
                assigneeName: 'Ali Saha',
                assignedAt: '2026-10-20T09:00:00Z',
                dueDate: null,
                isOverdue: false,
              },
            }),
          ),
      }),
    )
    renderPage('/assignments/new?itemId=item-1')

    expect(
      await screen.findByText('ITM-AAAAAA zaten Ali Saha adına zimmetli. Önce iade alın.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Elle giriş')).toBeInTheDocument()
  })
})
