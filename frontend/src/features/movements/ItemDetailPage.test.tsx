import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ItemDetail } from '@/api/types'
import { assignedItem, itemDetail } from '@/features/assignments/testData'
import { Permissions } from '@/features/auth/permissions'
import { ItemDetailPage } from '@/features/movements/ItemDetailPage'
import { FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

const manager = fakeUser({ role: 'WarehouseManager', permissions: [Permissions.CanMoveStock] })
const page = (items: unknown[] = []) =>
  json(200, { items, totalCount: items.length, page: 1, pageSize: 25 })

function renderItem(item: ItemDetail, user = manager) {
  vi.stubGlobal(
    'fetch',
    mockApi({
      'GET /api/items/item-1': () => json(200, item),
      'GET /api/movements': () => page(),
      'GET /api/assignments': () => page(),
    }),
  )
  renderRoutes(
    [
      {
        path: '/items/:id',
        element: (
          <FakeAuth user={user}>
            <ItemDetailPage />
          </FakeAuth>
        ),
      },
    ],
    '/items/item-1',
  )
}

const buttons = ['Zimmet ver', 'İade al', 'Tamire gönder', 'Tamirden döndü']
const visibleButtons = () =>
  buttons.filter(
    (name) => screen.queryByRole('button', { name }) ?? screen.queryByRole('link', { name }),
  )

describe('ItemDetailPage status actions', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('shows assign and repair for an in-stock item', async () => {
    renderItem(itemDetail())
    expect(await screen.findByText('ITM-AAAAAA')).toBeInTheDocument()
    expect(visibleButtons()).toEqual(['Zimmet ver', 'Tamire gönder'])
  })

  it('shows only return and the holder for an assigned item', async () => {
    renderItem(assignedItem())
    expect(await screen.findByText('Ali Saha')).toBeInTheDocument()
    expect(visibleButtons()).toEqual(['İade al'])
  })

  it('shows only back-from-repair for an item in repair', async () => {
    renderItem(
      itemDetail({ status: 'InRepair', availableActions: ['ReturnFromRepair', 'Dispose'] }),
    )
    expect(await screen.findByText('ITM-AAAAAA')).toBeInTheDocument()
    expect(visibleButtons()).toEqual(['Tamirden döndü'])
  })

  it('shows no actions without the move-stock permission', async () => {
    renderItem(itemDetail(), fakeUser())
    expect(await screen.findByText('ITM-AAAAAA')).toBeInTheDocument()
    expect(visibleButtons()).toEqual([])
  })
})
