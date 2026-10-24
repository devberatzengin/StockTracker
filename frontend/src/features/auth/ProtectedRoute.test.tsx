import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it } from 'vitest'
import { ProtectedRoute } from '@/features/auth/ProtectedRoute'

describe('ProtectedRoute', () => {
  it('renders child routes', () => {
    const router = createMemoryRouter(
      [{ element: <ProtectedRoute />, children: [{ path: '/', element: <p>İçerik</p> }] }],
      { initialEntries: ['/'] },
    )

    render(<RouterProvider router={router} />)

    expect(screen.getByText('İçerik')).toBeInTheDocument()
  })
})
