import { Navigate, Outlet, useLocation } from 'react-router'
import { FullPageLoader } from '@/components/FullPageLoader'
import { useAuth } from '@/features/auth/authContext'

/** Requires a signed-in user; otherwise sends them to /login and remembers where they were going. */
export function ProtectedRoute() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'loading') {
    return <FullPageLoader />
  }

  if (status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: location }} />
  }

  return <Outlet />
}
