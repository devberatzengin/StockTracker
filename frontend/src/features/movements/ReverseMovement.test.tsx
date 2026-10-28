import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Movement } from '@/api/types'
import { Permissions } from '@/features/auth/permissions'
import { MovementReversal } from '@/features/movements/ReverseMovement'
import { canReverse } from '@/features/movements/reversal'
import { FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

const manager = fakeUser({ role: 'WarehouseManager', permissions: [Permissions.CanMoveStock] })
const staff = fakeUser({ role: 'Staff', permissions: [] })

const receipt: Movement = {
  id: 'm-1',
  type: 'In',
  occurredAt: '2026-10-27T09:00:00Z',
  productId: 'p-1',
  productCode: 'PRD-AAAAAA',
  productName: 'Vida M4',
  unit: 'Piece',
  itemId: null,
  itemCode: null,
  serialNumber: null,
  fromLocationId: null,
  fromLocationName: null,
  toLocationId: 'l-1',
  toLocationName: 'Raf A1',
  quantity: 50,
  reason: undefined,
  note: null,
  referenceId: 'r-1',
  userId: 'u-1',
  userName: 'Mehmet Kaya',
  fromStatus: undefined,
  toStatus: undefined,
  assigneeName: null,
  reversesMovementId: null,
  reversedByMovementId: null,
}

function render(movement: Movement, user = manager) {
  renderRoutes(
    [{ path: '/', element: <FakeAuth user={user}><MovementReversal movement={movement} /></FakeAuth> }],
    '/',
  )
}

describe('canReverse', () => {
  it('allows quantity In/Out/Transfer and item In/Transfer only, once', () => {
    expect(canReverse(receipt)).toBe(true)
    expect(canReverse({ ...receipt, type: 'Out' })).toBe(true)
    expect(canReverse({ ...receipt, type: 'Out', itemId: 'i-1' })).toBe(false)
    expect(canReverse({ ...receipt, type: 'Transfer', itemId: 'i-1' })).toBe(true)
    expect(canReverse({ ...receipt, type: 'Adjustment' })).toBe(false)
    expect(canReverse({ ...receipt, type: 'Assign', itemId: 'i-1' })).toBe(false)
    expect(canReverse({ ...receipt, reversedByMovementId: 'm-2' })).toBe(false)
    expect(canReverse({ ...receipt, reversesMovementId: 'm-0' })).toBe(false)
  })
})

describe('MovementReversal', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('requires a note and posts the reversal', async () => {
    const posted: unknown[] = []
    vi.stubGlobal(
      'fetch',
      mockApi({
        'POST /api/movements/m-1/reverse': async (request) => {
          posted.push(await request.json())
          return json(200, { referenceId: 'r-2', movementCount: 1, createdItems: [] })
        },
      }),
    )
    const user = userEvent.setup()
    render(receipt)

    await user.click(screen.getByRole('button', { name: 'Ters kayıt' }))
    await user.click(await screen.findByRole('button', { name: 'Ters çevir' }))
    expect(await screen.findByText('Ters kayıt için not zorunludur.')).toBeInTheDocument()
    expect(posted).toHaveLength(0)

    await user.type(screen.getByRole('textbox', { name: /Not/ }), 'Yanlış raf')
    await user.click(screen.getByRole('button', { name: 'Ters çevir' }))

    await vi.waitFor(() => expect(posted).toEqual([{ note: 'Yanlış raf' }]))
  })

  it('shows badges instead of the button for reversed movements and hides it without permission', () => {
    render({ ...receipt, reversedByMovementId: 'm-2' })
    expect(screen.getByText('Ters çevrildi')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Ters kayıt' })).not.toBeInTheDocument()
  })

  it('hides the button for users who cannot move stock', () => {
    render(receipt, staff)
    expect(screen.queryByRole('button', { name: 'Ters kayıt' })).not.toBeInTheDocument()
  })
})
