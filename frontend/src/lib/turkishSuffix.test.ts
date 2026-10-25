import { describe, expect, it } from 'vitest'
import { ablative, accusative, dative } from '@/lib/turkishSuffix'

describe('Turkish case suffixes', () => {
  it.each([
    ['Raf A1', "Raf A1'den", "Raf A1'e", "Raf A1'i"],
    ['Raf B2', "Raf B2'den", "Raf B2'ye", "Raf B2'yi"],
    ['Vida M4', "Vida M4'ten", "Vida M4'e", "Vida M4'ü"],
    ['Kutu 3', "Kutu 3'ten", "Kutu 3'e", "Kutu 3'ü"],
    ['Raf 10', "Raf 10'dan", "Raf 10'a", "Raf 10'u"],
    ['Raf 40', "Raf 40'tan", "Raf 40'a", "Raf 40'ı"],
    ['Laptop', "Laptop'tan", "Laptop'a", "Laptop'u"],
    ['Depo', "Depo'dan", "Depo'ya", "Depo'yu"],
    ['Dolap', "Dolap'tan", "Dolap'a", "Dolap'ı"],
    ['Ana Bina', "Ana Bina'dan", "Ana Bina'ya", "Ana Bina'yı"],
    ['Mönitör', "Mönitör'den", "Mönitör'e", "Mönitör'ü"],
    ['Raf 100', "Raf 100'den", "Raf 100'e", "Raf 100'ü"],
  ])('%s', (name, abl, dat, acc) => {
    expect(ablative(name)).toBe(abl)
    expect(dative(name)).toBe(dat)
    expect(accusative(name)).toBe(acc)
  })
})
