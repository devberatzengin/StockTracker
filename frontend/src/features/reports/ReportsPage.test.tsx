import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ReportsPage } from '@/features/reports/ReportsPage'
import { json, renderRoutes } from '@/test/utils'

describe('ReportsPage', () => {
  const urls: string[] = []

  beforeEach(() => {
    urls.length = 0
    vi.stubGlobal(
      'URL',
      Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:xlsx'), revokeObjectURL: vi.fn() }),
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  it('downloads the movement report for the chosen days', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(new Blob(['xlsx']), {
          status: 200,
          headers: { 'Content-Type': 'application/octet-stream' },
        })
      }),
    )
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    const user = userEvent.setup()
    renderRoutes([{ path: '/', element: <ReportsPage /> }], '/')

    const from = screen.getByLabelText('Başlangıç')
    await user.clear(from)
    await user.type(from, '2026-10-01')
    const to = screen.getByLabelText('Bitiş')
    await user.clear(to)
    await user.type(to, '2026-10-15')
    await user.click(screen.getAllByRole('button', { name: 'Excel indir' })[1]!)

    await waitFor(() => expect(click).toHaveBeenCalled())
    expect(urls[0]).toContain('/api/reports/movements.xlsx?from=2026-10-01&to=2026-10-15')
  })

  it('explains the row limit', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        json(422, {
          title:
            'Rapor 60.000 satır içeriyor; en fazla 50.000 satır indirilebilir. Tarih aralığını daraltın.',
          code: 'Reports.TooManyRows',
        }),
      ),
    )
    const user = userEvent.setup()
    renderRoutes([{ path: '/', element: <ReportsPage /> }], '/')

    await user.click(screen.getAllByRole('button', { name: 'Excel indir' })[1]!)

    expect(await screen.findByText(/Tarih aralığını daraltın/)).toBeInTheDocument()
  })
})
