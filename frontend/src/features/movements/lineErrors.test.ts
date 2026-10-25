import { describe, expect, it } from 'vitest'
import { ApiError } from '@/api/client'
import { groupByReference } from '@/features/movements/grouping'
import { parseLineErrors } from '@/features/movements/lineErrors'
import { splitSerialNumbers } from '@/features/movements/stockInForm'

describe('parseLineErrors', () => {
  it('splits line, field and general errors', () => {
    const error = new ApiError(422, {
      status: 422,
      detail: 'Yetersiz stok',
      errors: {
        'lines[2].quantity': ['Yetersiz stok: mevcut 70, istenen 80.'],
        'lines[0].itemId': ['Adet bulunamadı.'],
        toLocationId: ['Hedef lokasyon seçin.'],
      },
    })

    const parsed = parseLineErrors(error)

    expect(parsed.lines[2]).toEqual({ quantity: 'Yetersiz stok: mevcut 70, istenen 80.' })
    expect(parsed.lines[0]).toEqual({ itemId: 'Adet bulunamadı.' })
    expect(parsed.fields).toEqual({ toLocationId: 'Hedef lokasyon seçin.' })
    expect(parsed.general[0]).toMatch(/Hiçbir hareket kaydedilmedi/)
  })

  it('falls back to the error message without field errors', () => {
    expect(
      parseLineErrors(
        new ApiError(409, { title: 'x', detail: 'Stok bu arada değişti, tekrar deneyin.' }),
      ).general,
    ).toEqual(['Stok bu arada değişti, tekrar deneyin.'])
  })
})

describe('groupByReference', () => {
  it('groups consecutive movements of the same operation', () => {
    const m = (id: string, referenceId: string) => ({ id, referenceId }) as never
    const groups = groupByReference([m('1', 'a'), m('2', 'a'), m('3', 'b'), m('4', 'a')])

    expect(groups.map((g) => [g.referenceId, g.movements.length])).toEqual([
      ['a', 2],
      ['b', 1],
      ['a', 1],
    ])
  })
})

describe('splitSerialNumbers', () => {
  it('splits pasted lists and drops duplicates', () => {
    expect(splitSerialNumbers(['SN-1,SN-2, SN-3', 'SN-2', 'SN-4;SN-5 '])).toEqual([
      'SN-1',
      'SN-2',
      'SN-3',
      'SN-4',
      'SN-5',
    ])
  })
})
