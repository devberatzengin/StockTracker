import { describe, expect, it } from 'vitest'
import { canAssign, canReturn } from '@/features/assignments/itemRules'
import { assignedItem, itemDetail } from '@/features/assignments/testData'

describe('item rules', () => {
  it('allows assigning only in-stock items of assignable products', () => {
    expect(canAssign(itemDetail())).toBeNull()
    expect(canAssign(itemDetail({ productIsAssignable: false }))).toBe(
      'Laptop zimmetlenebilir bir ürün değil.',
    )
    expect(
      canAssign(itemDetail({ status: 'InRepair', availableActions: ['ReturnFromRepair'] })),
    ).toBe('ITM-AAAAAA şu anki durumunda zimmetlenemez.')
  })

  it('allows returning only assigned items', () => {
    expect(canReturn(assignedItem())).toBeNull()
    expect(canReturn(itemDetail())).toBe('ITM-AAAAAA zimmetli değil; iade alınacak bir zimmet yok.')
  })
})
