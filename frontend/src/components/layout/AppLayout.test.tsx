import { MantineProvider } from '@mantine/core'
import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it } from 'vitest'
import { AppLayout } from '@/components/layout/AppLayout'

describe('AppLayout', () => {
  it('renders navigation and the active page', () => {
    const router = createMemoryRouter(
      [{ element: <AppLayout />, children: [{ path: '/dev/scan', element: <p>Sayfa</p> }] }],
      { initialEntries: ['/dev/scan'] },
    )

    render(
      <MantineProvider>
        <RouterProvider router={router} />
      </MantineProvider>,
    )

    expect(screen.getByText('Sayfa')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Tarama denemesi/ })).toHaveAttribute(
      'href',
      '/dev/scan',
    )
  })
})
