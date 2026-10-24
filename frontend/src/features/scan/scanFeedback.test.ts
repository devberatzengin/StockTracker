import { describe, expect, it } from 'vitest'
import { createDuplicateFilter } from '@/features/scan/scanFeedback'

describe('createDuplicateFilter', () => {
  it('ignores the same value within the window and accepts it afterwards', () => {
    let now = 0
    const isNew = createDuplicateFilter(2000, () => now)

    expect(isNew('PRD-ABCDEF')).toBe(true)
    now = 1500
    expect(isNew('PRD-ABCDEF')).toBe(false)
    now = 3600
    expect(isNew('PRD-ABCDEF')).toBe(true)
  })

  it('accepts a different value immediately', () => {
    let now = 0
    const isNew = createDuplicateFilter(2000, () => now)

    isNew('PRD-ABCDEF')
    now = 100
    expect(isNew('LOC-123456')).toBe(true)
  })
})
