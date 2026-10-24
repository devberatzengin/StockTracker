import { Outlet } from 'react-router'

/**
 * Guards routes that require a signed-in user.
 * Allows everything for now; authentication checks arrive in Phase 1.1.
 */
export function ProtectedRoute() {
  return <Outlet />
}
