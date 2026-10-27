import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { json, mockApi, renderRoutes } from '@/test/utils'

const day = (date: string, counts: Record<string, number> = {}) => ({
  date,
  in: 0,
  out: 0,
  transfer: 0,
  adjustment: 0,
  assign: 0,
  return: 0,
  statusChange: 0,
  ...counts,
})

const dashboard = {
  cards: {
    activeProducts: 3,
    totalItems: 12,
    lowStockProducts: 2,
    openAssignments: 4,
    overdueAssignments: 1,
    itemsInRepair: 1,
  },
  dailyMovements: [day('2026-10-26'), day('2026-10-27', { in: 5, out: 1 })],
  topProducts: [{ productId: 'laptop', code: 'PRD-LAPTOP', name: 'Laptop', movementCount: 5 }],
  itemStatuses: { inStock: 9, assigned: 2, inRepair: 1 },
  lowStock: [
    {
      productId: 'screw',
      code: 'PRD-SCREW1',
      name: 'Vida M4',
      unit: 'Piece',
      total: 45,
      minStockLevel: 100,
      shortage: 55,
    },
  ],
  overdueAssignments: [
    {
      assignmentId: 'a1',
      itemId: 'item-1',
      itemCode: 'ITM-AAAAAA',
      productName: 'Laptop',
      assigneeName: 'Ali Saha',
      dueDate: '2026-10-24',
      daysOverdue: 3,
    },
  ],
}

describe('DashboardPage', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows the cards and both lists', async () => {
    vi.stubGlobal('fetch', mockApi({ 'GET /api/dashboard': () => json(200, dashboard) }))
    renderRoutes([{ path: '/', element: <DashboardPage /> }], '/')

    expect(await screen.findByText('Kritik stok')).toBeInTheDocument()
    expect(screen.getByText('12')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Vida M4' })).toHaveAttribute('href', '/products/screw')
    expect(screen.getByText('55 adet')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'ITM-AAAAAA' })).toHaveAttribute(
      'href',
      '/items/item-1',
    )
    expect(screen.getByText('3 gün')).toBeInTheDocument()
  })

  it('says when there was no movement', async () => {
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/dashboard': () =>
          json(200, {
            ...dashboard,
            dailyMovements: [day('2026-10-27')],
            topProducts: [],
            lowStock: [],
            overdueAssignments: [],
          }),
      }),
    )
    renderRoutes([{ path: '/', element: <DashboardPage /> }], '/')

    expect(await screen.findByText('Son 30 günde hareket yok.')).toBeInTheDocument()
    expect(screen.getByText('Kritik stokta ürün yok.')).toBeInTheDocument()
    expect(screen.getByText('Gecikmiş zimmet yok.')).toBeInTheDocument()
  })
})
