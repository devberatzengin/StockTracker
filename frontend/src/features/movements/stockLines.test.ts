import { describe, expect, it } from 'vitest'
import { validateStockLines, type EditorLine } from '@/features/movements/stockLines'

const screw = {
  id: 'screw',
  code: 'PRD-SCREW1',
  name: 'Vida M4',
  trackingType: 'Quantity' as const,
  unit: 'Piece' as const,
}
const laptop = { ...screw, id: 'laptop', name: 'Laptop', trackingType: 'Serialized' as const }

const quantity = (
  overrides: Partial<Extract<EditorLine, { kind: 'quantity' }>> = {},
): EditorLine => ({
  key: crypto.randomUUID(),
  kind: 'quantity',
  product: screw,
  fromLocationId: 'a1',
  quantity: 5,
  ...overrides,
})

describe('validateStockLines', () => {
  it('needs at least one line', () => {
    expect(validateStockLines([]).general).toEqual(['En az bir satır ekleyin.'])
  })

  it('names each broken field of each line', () => {
    const errors = validateStockLines([
      quantity(),
      quantity({ product: null, fromLocationId: null, quantity: '' }),
      quantity({ product: laptop }),
      quantity({ quantity: 0 }),
    ])

    expect(errors.lines[0]).toBeUndefined()
    expect(errors.lines[1]).toEqual({
      productId: 'Ürün seçin.',
      fromLocationId: 'Kaynak lokasyon seçin.',
      quantity: 'Miktar sıfırdan büyük olmalıdır.',
    })
    expect(errors.lines[2]?.productId).toMatch(/Adet ekle/)
    expect(errors.lines[3]?.quantity).toBe('Miktar sıfırdan büyük olmalıdır.')
    expect(errors.general).toEqual(['İşaretli satırları düzeltin.'])
  })

  it('rejects a transfer to the line’s own source', () => {
    const errors = validateStockLines([quantity({ fromLocationId: 'b2' })], 'b2')

    expect(errors.lines[0]?.fromLocationId).toBe('Kaynak ve hedef lokasyon aynı olamaz.')
  })

  it('accepts scanned items without further fields', () => {
    const item: EditorLine = {
      key: 'k',
      kind: 'item',
      itemId: 'i1',
      code: 'ITM-AAAAAA',
      title: 'ITM-AAAAAA',
      subtitle: null,
    }

    expect(validateStockLines([item], 'b2')).toEqual({ lines: {}, general: [], fields: {} })
  })
})
