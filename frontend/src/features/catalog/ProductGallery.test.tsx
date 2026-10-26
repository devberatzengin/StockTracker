import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ProductPhoto } from '@/api/types'
import { move } from '@/features/catalog/galleryOrder'
import { ProductGallery } from '@/features/catalog/ProductGallery'
import { empty, json, mockApi, renderRoutes } from '@/test/utils'

const photo = (id: string, isPrimary = false, sortOrder = 0): ProductPhoto => ({
  id,
  url: `/api/files/products/p1/${id}.webp?exp=1&sig=x`,
  thumbnailUrl: `/api/files/products/p1/${id}_thumb.webp?exp=1&sig=x`,
  isPrimary,
  sortOrder,
  sizeBytes: 1000,
})

describe('ProductGallery', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('uploads the chosen file as multipart and opens the phone camera', async () => {
    // Inspect the raw fetch call: the body must be the FormData itself (multipart), not JSON.
    const uploads: FormData[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, init: RequestInit) => {
        uploads.push(init.body as FormData)
        return json(200, photo('new', true))
      }),
    )
    const user = userEvent.setup()
    const { container } = renderRoutes(
      [{ path: '/', element: <ProductGallery productId="p1" photos={[]} canManage /> }],
      '/',
    )

    const input = container.querySelector('input[type="file"]') as HTMLInputElement
    expect(input).toHaveAttribute('capture', 'environment')
    expect(input).toHaveAttribute('accept', 'image/*')
    await user.upload(input, new File(['jpeg-bytes'], 'kamera.jpg', { type: 'image/jpeg' }))

    await waitFor(() => expect(uploads).toHaveLength(1))
    expect(uploads[0]).toBeInstanceOf(FormData)
    expect((uploads[0]!.get('file') as File).name).toBe('kamera.jpg')
  })

  it('shows the server message for a rejected file', async () => {
    vi.stubGlobal(
      'fetch',
      mockApi({
        'POST /api/products/p1/photos': () =>
          json(400, {
            title: 'Bazı alanlar hatalı.',
            errors: { file: ['Sadece JPEG, PNG ya da WebP fotoğraf yükleyebilirsiniz.'] },
          }),
      }),
    )
    const user = userEvent.setup()
    const { container } = renderRoutes(
      [{ path: '/', element: <ProductGallery productId="p1" photos={[]} canManage /> }],
      '/',
    )

    await user.upload(
      container.querySelector('input[type="file"]') as HTMLInputElement,
      new File(['x'], 'a.jpg', { type: 'image/jpeg' }),
    )

    expect(
      await screen.findByText('Sadece JPEG, PNG ya da WebP fotoğraf yükleyebilirsiniz.'),
    ).toBeInTheDocument()
  })

  it('makes a photo primary and moves it with the arrows', async () => {
    const calls: string[] = []
    const orders: unknown[] = []
    vi.stubGlobal(
      'fetch',
      mockApi({
        'POST /api/products/p1/photos/b/primary': (request) => {
          calls.push(request.url)
          return empty(204)
        },
        'PUT /api/products/p1/photos/order': async (request) => {
          orders.push(await request.json())
          return empty(204)
        },
      }),
    )
    const user = userEvent.setup()
    renderRoutes(
      [
        {
          path: '/',
          element: (
            <ProductGallery
              productId="p1"
              photos={[photo('a', true, 0), photo('b', false, 1)]}
              canManage
            />
          ),
        },
      ],
      '/',
    )

    expect(within(screen.getByTestId('photo-a')).getByText('Ana')).toBeInTheDocument()
    await user.click(within(screen.getByTestId('photo-b')).getByRole('button', { name: 'Ana yap' }))
    await user.click(screen.getByRole('button', { name: 'Fotoğraf 2 sola taşı' }))

    await waitFor(() => expect(orders).toEqual([{ photoIds: ['b', 'a'] }]))
    expect(calls).toHaveLength(1)
    expect(screen.getByRole('button', { name: 'Fotoğraf 1 sola taşı' })).toBeDisabled()
  })

  it('hides editing for read-only users', () => {
    renderRoutes(
      [
        {
          path: '/',
          element: <ProductGallery productId="p1" photos={[photo('a', true)]} canManage={false} />,
        },
      ],
      '/',
    )

    expect(screen.queryByRole('button', { name: 'Fotoğraf ekle' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /sil/ })).not.toBeInTheDocument()
  })

  it('moves items in a list', () => {
    expect(move(['a', 'b', 'c'], 2, 0)).toEqual(['c', 'a', 'b'])
    expect(move(['a', 'b', 'c'], 0, 5)).toEqual(['a', 'b', 'c'])
  })
})
