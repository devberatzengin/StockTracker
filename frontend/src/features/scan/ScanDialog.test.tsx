import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ScanDialog } from '@/features/scan/ScanDialog'
import { renderRoutes } from '@/test/utils'

function renderDialog(
  onScan: (value: string, format: unknown) => void | Promise<void>,
  mode?: 'single' | 'continuous',
) {
  return renderRoutes(
    [{ path: '/', element: <ScanDialog opened onClose={() => {}} onScan={onScan} mode={mode} /> }],
    '/',
  )
}

describe('ScanDialog', () => {
  it('explains when the camera is unavailable and accepts manual entry', async () => {
    const onScan = vi.fn()
    const user = userEvent.setup()
    renderDialog(onScan)

    // jsdom has no camera, which is like an insecure context.
    expect(await screen.findByText('Kamera kullanılamıyor')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Elle giriş'), '  prd-abcdef {Enter}')

    expect(onScan).toHaveBeenCalledWith('prd-abcdef', null)
    expect(screen.getByLabelText('Elle giriş')).toHaveValue('')
  })

  it('ignores an empty manual entry', async () => {
    const onScan = vi.fn()
    const user = userEvent.setup()
    renderDialog(onScan)

    await user.click(await screen.findByRole('button', { name: 'Bul' }))

    expect(onScan).not.toHaveBeenCalled()
  })

  it('in single mode ignores new entries while the previous one is being handled', async () => {
    let finish!: () => void
    const onScan = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          finish = resolve
        }),
    )
    const user = userEvent.setup()
    renderDialog(onScan, 'single')

    await user.type(await screen.findByLabelText('Elle giriş'), 'PRD-AAAAAA{Enter}')
    await user.type(screen.getByLabelText('Elle giriş'), 'PRD-BBBBBB{Enter}')
    expect(onScan).toHaveBeenCalledTimes(1)

    finish()
    await user.type(screen.getByLabelText('Elle giriş'), 'PRD-CCCCCC{Enter}')
    await vi.waitFor(() => expect(onScan).toHaveBeenCalledTimes(2))
  })

  it('in continuous mode handles entries one after another', async () => {
    const onScan = vi.fn()
    const user = userEvent.setup()
    renderDialog(onScan, 'continuous')

    await user.type(await screen.findByLabelText('Elle giriş'), 'PRD-AAAAAA{Enter}')
    await user.type(screen.getByLabelText('Elle giriş'), 'PRD-BBBBBB{Enter}')

    expect(onScan).toHaveBeenCalledTimes(2)
    expect(screen.getByText(/Sürekli mod/)).toBeInTheDocument()
  })
})
