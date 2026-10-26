import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { countDetail, countReview } from '@/features/counts/testData'
import { ReviewScreen } from '@/features/counts/ReviewScreen'
import { json, mockApi, renderRoutes } from '@/test/utils'

describe('ReviewScreen', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('needs a decision on every difference; "apply to all" fills a group', async () => {
    const approvals: unknown[] = []
    vi.stubGlobal(
      'fetch',
      mockApi({
        'GET /api/counts/count-1/review': () => json(200, countReview(1)),
        'GET /api/movements': () => json(200, { items: [], totalCount: 1, page: 1, pageSize: 10 }),
        'POST /api/counts/count-1/approve': async (request) => {
          approvals.push(await request.json())
          return json(200, { referenceId: 'ref-1', movementCount: 2 })
        },
        'GET /api/counts/count-1': () => json(200, countDetail({ status: 'Approved' })),
        'GET /api/counts': () => json(200, { items: [], totalCount: 0, page: 1, pageSize: 25 }),
      }),
    )
    const user = userEvent.setup()
    renderRoutes(
      [{ path: '/c', element: <ReviewScreen count={countDetail({ status: 'Reviewing' })} /> }],
      '/c',
    )

    expect(
      await screen.findByText('Sayım başladıktan sonra kapsamda hareket oldu'),
    ).toBeInTheDocument()
    expect(screen.getByText('−15')).toBeInTheDocument()
    expect(screen.getByText('2 satır kararsız')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Onayla' })).toBeDisabled()

    // Decide the screw line by hand, the laptop via "apply to all".
    await user.click(screen.getAllByText('Farkı uygula').at(-1)!)
    await user.click(screen.getByRole('button', { name: 'Buraya taşı' }))
    expect(screen.queryByText(/satır kararsız/)).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Onayla' }))
    await waitFor(() =>
      expect(approvals[0]).toEqual({
        decisions: [
          { lineId: 'line-screw', resolution: 'ApplyDifference' },
          { lineId: 'line-laptop', resolution: 'MoveHere' },
        ],
      }),
    )
  })
})
