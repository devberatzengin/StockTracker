import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { refreshSession, setSessionHandlers, tokenStore } from '@/api/client'
import type { CurrentUser } from '@/api/types'
import { authApi } from '@/features/auth/authApi'
import { AuthContext, type AuthContextValue, type AuthStatus } from '@/features/auth/authContext'
import type { Permission } from '@/features/auth/permissions'

const REFRESH_LOCK = 'stocktracker-auth-refresh'

/**
 * Serializes refreshes across browser tabs. The refresh cookie is shared, and the backend
 * treats a rotated token presented twice as theft, so two tabs must not refresh at once.
 */
async function refreshAccessToken(): Promise<boolean> {
  const refresh = async () => {
    try {
      const { accessToken } = await authApi.refresh()
      tokenStore.set(accessToken)
      return true
    } catch {
      tokenStore.set(null)
      return false
    }
  }

  return navigator.locks ? navigator.locks.request(REFRESH_LOCK, refresh) : refresh()
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const location = useLocation()
  const locationRef = useRef(location)
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUser] = useState<CurrentUser | null>(null)

  useEffect(() => {
    locationRef.current = location
  }, [location])

  const clearSession = useCallback(() => {
    tokenStore.set(null)
    setUser(null)
    setStatus('anonymous')
    queryClient.clear()
  }, [queryClient])

  useEffect(() => {
    setSessionHandlers({
      refresh: refreshAccessToken,
      onExpired: () => {
        clearSession()
        const from = locationRef.current
        if (from.pathname !== '/login') {
          navigate('/login', { replace: true, state: { from } })
        }
      },
    })
  }, [clearSession, navigate])

  // Silent sign-in on startup using the refresh cookie. refreshSession() is single-flight,
  // so StrictMode's double effect run still sends one request.
  useEffect(() => {
    let cancelled = false
    void (async () => {
      const refreshed = await refreshSession()
      const me = refreshed ? await authApi.me().catch(() => null) : null
      if (cancelled) return
      setUser(me)
      setStatus(me ? 'authenticated' : 'anonymous')
    })()
    return () => {
      cancelled = true
    }
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const { accessToken } = await authApi.login(email, password)
    tokenStore.set(accessToken)
    const me = await authApi.me()
    setUser(me)
    setStatus('authenticated')
  }, [])

  const logout = useCallback(async () => {
    await authApi.logout().catch(() => undefined)
    clearSession()
    navigate('/login', { replace: true })
  }, [clearSession, navigate])

  const changePassword = useCallback(async (currentPassword: string, newPassword: string) => {
    const { accessToken } = await authApi.changePassword(currentPassword, newPassword)
    tokenStore.set(accessToken)
  }, [])

  const hasPermission = useCallback(
    (permission: Permission) => user?.permissions.includes(permission) ?? false,
    [user],
  )

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, login, logout, changePassword, hasPermission }),
    [status, user, login, logout, changePassword, hasPermission],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
