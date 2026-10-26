import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CountingScreen } from '@/features/counts/CountingScreen'
import { CountPage } from '@/features/counts/CountPage'
import { countDetail } from '@/features/counts/testData'
import { empty, FakeAuth, fakeUser, json, mockApi, renderRoutes } from '@/test/utils'

const progress = { expectedLines: 1, countedExpectedLines: 1, unexpectedLines: 0 }

describe('CountingScreen', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('sets the shelf from its label, asks for a quantity and undoes the last scan', async () => {
    const scans: unknown[] = []
    const undone: string[] = []
    vi.stubGlobal(
      'fetch',
      mockApi({
        'POST /api/counts/count-1/scan': async (request) => {
          const body = (await request.json()) as { code: string; quantity?: number }
          scans.push(body)
          if (body.code === 'LOC-B20000') {
            return json(200, {
              kind: 'Location',
              title: 'Raf B2',
              locationId: 'b2',
              locationPath: 'Bina › Raf B2',
              progress,
            })
          }
          return body.quantity
            ? json(200, {
                kind: 'Counted',
                title: 'Vida M4',
                productId: 'screw',
                unit: 'Piece',
                lineId: 'line-screw',
                countedQuantity: 185,
                progress,
              })
            : json(200, {
                kind: 'QuantityRequired',
                title: 'Vida M4',
                productId: 'screw',
                unit: 'Piece',
                progress,
              })
        },
        'GET /api/counts/count-1': () =>
          json(
            200,
            countDetail({
              recentScans: [
                {
                  id: 's1',
                  lineId: 'line-screw',
                  sequence: 1,
                  quantity: 185,
                  scannedAt: '2026-10-26T09:05:00Z',
                  scannedByName: 'Ali',
                  productName: 'Vida M4',
                  unit: 'Piece',
                  itemCode: undefined,
                  locationName: 'Raf B2',
                  lineType: 'Expected',
                  canUndo: true,
                },
              ],
            }),
          ),
        'DELETE /api/counts/count-1/lines/line-screw/last-scan': (request) => {
          undone.push(request.url)
          return empty(204)
        },
      }),
    )
    const user = userEvent.setup()
    renderRoutes(
      [
        {
          path: '/counts/:id',
          element: (
            <FakeAuth user={fakeUser()}>
              <CountPage />
            </FakeAuth>
          ),
        },
      ],
      '/counts/count-1',
    )

    // No context yet for a count with sub-locations.
    expect(await screen.findByText('Rafın etiketini okutun')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Elle giriş'), 'LOC-B20000{Enter}')
    expect(await screen.findByText('Bulunduğunuz yer: Bina › Raf B2')).toBeInTheDocument()

    // A quantity product asks for the counted quantity.
    await user.type(screen.getByLabelText('Elle giriş'), 'PRD-SCREW1{Enter}')
    await user.type(await screen.findByLabelText(/Sayılan miktar/), '185')
    await user.click(screen.getByRole('button', { name: 'Ekle' }))
    expect(await screen.findByText('Sayıldı: Vida M4 (toplam 185 adet)')).toBeInTheDocument()
    expect(scans).toEqual([
      { locationId: null, code: 'LOC-B20000' },
      { locationId: 'b2', code: 'PRD-SCREW1' },
      { locationId: 'b2', code: 'PRD-SCREW1', quantity: 185 },
    ])

    // The recent scans list (refetched) offers undo for the last scan.
    await user.click(await screen.findByRole('button', { name: 'Geri al' }))
    await waitFor(() => expect(undone).toHaveLength(1))
  })

  it('warns about an unexpected item that is assigned', async () => {
    vi.stubGlobal(
      'fetch',
      mockApi({
        'POST /api/counts/count-1/scan': () =>
          json(200, {
            kind: 'Unexpected',
            title: 'ITM-AAAAAA · Laptop',
            lineId: 'l2',
            countedQuantity: 1,
            itemStatus: 'Assigned',
            itemLocationPath: undefined,
            progress,
          }),
        'GET /api/counts/count-1': () => json(200, countDetail()),
      }),
    )
    const user = userEvent.setup()
    renderRoutes(
      [
        {
          path: '/c',
          element: (
            <CountingScreen
              count={countDetail({
                includeChildren: false,
                locationId: 'b2',
                locationName: 'Raf B2',
              })}
            />
          ),
        },
      ],
      '/c',
    )

    // A single-location count already knows where the counter is.
    expect(screen.getByText('Raf B2')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Elle giriş'), 'ITM-AAAAAA{Enter}')
    expect(await screen.findByText('Beklenmeyen: ITM-AAAAAA · Laptop')).toBeInTheDocument()
    expect(screen.getByText('Bu adet zimmetli.')).toBeInTheDocument()
  })
})
