import { describe, expect, it } from 'vitest'
import { describeMovement } from '@/features/movements/movementText'

const base = {
  userName: 'Berat',
  productName: 'Vida M4',
  unit: 'Piece' as const,
  quantity: 200,
  itemCode: undefined,
  fromLocationName: 'Raf A1',
  toLocationName: 'Raf B2',
  reason: undefined,
  fromStatus: undefined,
  toStatus: undefined,
}

describe('describeMovement', () => {
  it('describes a quantity transfer', () => {
    expect(describeMovement({ ...base, type: 'Transfer' })).toBe(
      "Berat, 200 adet Vida M4'ü Raf A1'den Raf B2'ye taşıdı.",
    )
  })

  it('describes an in movement with decimals and units', () => {
    expect(
      describeMovement({
        ...base,
        type: 'In',
        unit: 'Kg',
        quantity: 12.5,
        productName: 'Kablo',
        fromLocationName: null,
      }),
    ).toBe("Berat, 12,5 kg Kablo'yu Raf B2'ye ekledi.")
  })

  it('describes an item out with its reason', () => {
    expect(
      describeMovement({
        ...base,
        type: 'Out',
        productName: 'Laptop',
        itemCode: 'ITM-7F3K9Q',
        quantity: 1,
        toLocationName: undefined,
        reason: 'Damaged',
      }),
    ).toBe("Berat, ITM-7F3K9Q kodlu Laptop'u Raf A1'den çıkardı (hasarlı).")
  })

  it('describes phase 2 movement types', () => {
    expect(
      describeMovement({
        ...base,
        type: 'Assign',
        itemCode: 'ITM-1',
        productName: 'Laptop',
        quantity: 1,
      }),
    ).toBe("Berat, ITM-1 kodlu Laptop'u zimmetledi.")
    expect(
      describeMovement({
        ...base,
        type: 'StatusChange',
        itemCode: 'ITM-1',
        productName: 'Laptop',
        quantity: 1,
        fromStatus: 'InStock',
        toStatus: 'InRepair',
      }),
    ).toBe('Berat, ITM-1 kodlu Laptop\'u "Stokta" durumundan "Tamirde" durumuna aldı.')
  })
})
