import { Outlet } from 'react-router'
import { ForbiddenPage } from '@/components/ForbiddenPage'
import { usePermission } from '@/features/auth/authContext'
import type { Permission } from '@/features/auth/permissions'

/** Shows the 403 screen when the signed-in user lacks the policy. */
export function RequirePermission({ permission }: { permission: Permission }) {
  return usePermission(permission) ? <Outlet /> : <ForbiddenPage />
}
