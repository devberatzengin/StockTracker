import { describe, expect, it } from 'vitest'
import { formatDay, todayIstanbul } from '@/lib/dates'

describe('calendar days', () => {
  it('uses the Istanbul day, which starts at 21:00 UTC', () => {
    expect(todayIstanbul(0, new Date('2026-10-25T20:59:00Z'))).toBe('2026-10-25')
    expect(todayIstanbul(0, new Date('2026-10-25T21:00:00Z'))).toBe('2026-10-26')
    expect(todayIstanbul(7, new Date('2026-10-25T09:00:00Z'))).toBe('2026-11-01')
  })

  it('formats a due date without shifting the day', () => {
    expect(formatDay('2026-11-01')).toBe('1 Kas 2026')
  })
})
