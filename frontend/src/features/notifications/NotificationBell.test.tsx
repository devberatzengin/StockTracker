import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Permissions } from '@/features/auth/permissions'
import { NotificationBell } from '@/features/notifications/NotificationBell'
import { notificationLink } from '@/features/notifications/notificationLinks'
import { empty, FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

const notification = (overrides: Record<string, unknown> = {}) => ({
  id: 'n1',
  type: 'LowStock',
  title: 'Kritik stok: Vida M4',
  message: 'Vida M4 (PRD-SCREW1) stoğu 90 adet; minimum 100 adet.',
  entityType: 'Product',
  entityId: 'screw',
  isRead: false,
  createdAt: '2026-10-27T09:00:00Z',
  resolvedAt: undefined,
  ...overrides,
})

const manager = fakeUser({ role: 'WarehouseManager', permissions: [Permissions.CanMoveStock] })

describe('NotificationBell', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows the unread count, lists notifications and opens the product', async () => {
    const read: string[] = []
    let unread = 2
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/notifications/unread-count': () => json(200, { count: unread }),
        'GET /api/notifications': () =>
          json(200, {
            items: [
              notification(),
              notification({
                id: 'n2',
                title: 'Eski',
                isRead: true,
                resolvedAt: '2026-10-27T10:00:00Z',
              }),
            ],
            totalCount: 2,
            page: 1,
            pageSize: 8,
          }),
        'POST /api/notifications/n1/read': (request) => {
          read.push(request.url)
          unread = 1
          return empty(204)
        },
      }),
    )
    const user = userEvent.setup()
    const { router } = renderRoutes(
      [
        {
          path: '/',
          element: (
            <FakeAuth user={manager}>
              <NotificationBell />
            </FakeAuth>
          ),
        },
        { path: '/products/:id', element: <p>Ürün sayfası</p> },
      ],
      '/',
    )

    const bell = await screen.findByRole('button', { name: 'Bildirimler (2 okunmamış)' })
    await user.click(bell)
    expect(await screen.findByText('Kritik stok: Vida M4')).toBeInTheDocument()
    expect(screen.getByText('Çözüldü')).toBeInTheDocument()

    await user.click(screen.getByText('Kritik stok: Vida M4'))

    await waitFor(() => expect(router.state.location.pathname).toBe('/products/screw'))
    expect(read).toHaveLength(1)
  })

  it('marks everything read', async () => {
    let unread = 3
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/notifications/unread-count': () => json(200, { count: unread }),
        'GET /api/notifications': () =>
          json(200, { items: [notification()], totalCount: 1, page: 1, pageSize: 8 }),
        'POST /api/notifications/read-all': () => {
          unread = 0
          return empty(204)
        },
      }),
    )
    const user = userEvent.setup()
    renderRoutes(
      [
        {
          path: '/',
          element: (
            <FakeAuth user={manager}>
              <NotificationBell />
            </FakeAuth>
          ),
        },
      ],
      '/',
    )

    await user.click(await screen.findByRole('button', { name: 'Bildirimler (3 okunmamış)' }))
    await user.click(await screen.findByRole('button', { name: 'Tümünü okundu yap' }))

    expect(await screen.findByRole('button', { name: 'Bildirimler' })).toBeInTheDocument()
  })

  it('links assignment reminders to the right list', () => {
    const overdue = notification({ type: 'AssignmentOverdue', entityType: 'Assignment' }) as never
    const dueSoon = notification({ type: 'AssignmentDueSoon', entityType: 'Assignment' }) as never
    expect(notificationLink(overdue, true)).toBe('/assignments')
    expect(notificationLink(overdue, false)).toBe('/my-assignments')
    expect(notificationLink(dueSoon, true)).toBe('/my-assignments')
  })
})
