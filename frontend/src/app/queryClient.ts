import { notifications } from '@mantine/notifications'
import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { getErrorMessage, isApiError } from '@/api/client'

declare module '@tanstack/react-query' {
  interface Register {
    queryMeta: { skipGlobalError?: boolean }
    mutationMeta: { skipGlobalError?: boolean }
  }
}

function showError(error: unknown, meta?: { skipGlobalError?: boolean }) {
  // 401 after a failed refresh redirects to the login page; no toast needed.
  if (meta?.skipGlobalError || isApiError(error, 401)) {
    return
  }
  notifications.show({
    color: 'red',
    title: 'Hata',
    message: getErrorMessage(error),
  })
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: (error, query) => showError(error, query.meta) }),
  mutationCache: new MutationCache({
    onError: (error, _variables, _context, mutation) => showError(error, mutation.meta),
  }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // Retry once for network and server errors only; 4xx answers will not change.
      retry: (failureCount, error) =>
        failureCount < 1 && (!isApiError(error) || error.status === 0 || error.status >= 500),
    },
  },
})
