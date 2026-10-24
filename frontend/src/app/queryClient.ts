import { notifications } from '@mantine/notifications'
import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { getErrorMessage } from '@/api/client'

function showError(error: unknown) {
  notifications.show({
    color: 'red',
    title: 'Hata',
    message: getErrorMessage(error),
  })
}

export const queryClient = new QueryClient({
  queryCache: new QueryCache({ onError: showError }),
  mutationCache: new MutationCache({ onError: showError }),
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
})
