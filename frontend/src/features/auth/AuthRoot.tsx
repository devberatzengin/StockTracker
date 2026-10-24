import { Outlet } from 'react-router'
import { AuthProvider } from '@/features/auth/AuthProvider'

/** Root route element: AuthProvider must live inside the router to navigate on session expiry. */
export function AuthRoot() {
  return (
    <AuthProvider>
      <Outlet />
    </AuthProvider>
  )
}
