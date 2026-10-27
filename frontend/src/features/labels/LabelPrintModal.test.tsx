import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LabelPrintModal } from '@/features/labels/LabelPrintModal'
import { json, renderRoutes } from '@/test/utils'

describe('LabelPrintModal', () => {
  const tab = { location: { href: '' }, close: vi.fn() }
  const bodies: unknown[] = []

  beforeEach(() => {
    bodies.length = 0
    vi.stubGlobal(
      'open',
      vi.fn(() => tab),
    )
    vi.stubGlobal(
      'URL',
      Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:pdf'), revokeObjectURL: vi.fn() }),
    )
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init: RequestInit) => {
        bodies.push(JSON.parse(init.body as string))
        return new Response(new Blob(['%PDF-1.7'], { type: 'application/pdf' }), {
          status: 200,
          headers: { 'Content-Type': 'application/pdf' },
        })
      }),
    )
  })
  afterEach(() => vi.unstubAllGlobals())

  const renderModal = (onClose = vi.fn()) =>
    renderRoutes(
      [
        {
          path: '/',
          element: (
            <LabelPrintModal
              opened
              onClose={onClose}
              target={{ locationId: 'room-1' }}
              defaultTemplate="Sheet2x4"
            />
          ),
        },
      ],
      '/',
    )

  it('starts at the chosen cell of a half-used sheet and opens the PDF', async () => {
    const onClose = vi.fn()
    const user = userEvent.setup()
    renderModal(onClose)

    expect(screen.getByText(/%100 ölçek/)).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /^Hücre / })).toHaveLength(8)
    await user.click(screen.getByRole('button', { name: 'Hücre 5' }))
    expect(screen.getByText('Başlangıç pozisyonu: 5')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'PDF oluştur' }))

    await waitFor(() => expect(onClose).toHaveBeenCalled())
    expect(bodies[0]).toEqual({ template: 'Sheet2x4', startPosition: 5, locationId: 'room-1' })
    expect(tab.location.href).toBe('blob:pdf')
  })

  it('has no start position for the thermal roll', async () => {
    const user = userEvent.setup()
    renderModal()

    await user.click(screen.getByRole('radio', { name: /Termal 50×30/ }))

    expect(screen.queryByRole('button', { name: /^Hücre / })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'PDF oluştur' }))
    await waitFor(() =>
      expect(bodies[0]).toEqual({
        template: 'Thermal50x30',
        startPosition: 1,
        locationId: 'room-1',
      }),
    )
  })

  it('shows the server message and closes the empty tab on failure', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        json(422, {
          title: 'Etiket basmak için PUBLIC_BASE_URL ayarlanmalı.',
          code: 'Labels.BaseUrlMissing',
        }),
      ),
    )
    const user = userEvent.setup()
    renderModal()

    await user.click(screen.getByRole('button', { name: 'PDF oluştur' }))

    expect(await screen.findByText(/PUBLIC_BASE_URL/)).toBeInTheDocument()
    expect(tab.close).toHaveBeenCalled()
  })
})
