import { apiClient } from '@/api/client'
import type { AccessTokenResponse, CurrentUser } from '@/api/types'

export const authApi = {
  login: (email: string, password: string) =>
    apiClient.post<AccessTokenResponse>('/auth/login', { email, password }),
  refresh: () => apiClient.post<AccessTokenResponse>('/auth/refresh'),
  logout: () => apiClient.post<void>('/auth/logout'),
  me: () => apiClient.get<CurrentUser>('/auth/me'),
  changePassword: (currentPassword: string, newPassword: string) =>
    apiClient.post<AccessTokenResponse>('/auth/change-password', { currentPassword, newPassword }),
}
