import { createBrowserRouter } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { NotFoundPage } from '@/components/NotFoundPage'
import { AuthRoot } from '@/features/auth/AuthRoot'
import { LoginPage } from '@/features/auth/LoginPage'
import { Permissions } from '@/features/auth/permissions'
import { ProtectedRoute } from '@/features/auth/ProtectedRoute'
import { RequirePermission } from '@/features/auth/RequirePermission'
import { HomePage } from '@/features/home/HomePage'

export const router = createBrowserRouter([
  {
    element: <AuthRoot />,
    children: [
      { path: '/login', element: <LoginPage /> },
      {
        element: <ProtectedRoute />,
        children: [
          {
            element: <AppLayout />,
            children: [
              { index: true, element: <HomePage /> },
              {
                element: <RequirePermission permission={Permissions.CanManageUsers} />,
                children: [
                  {
                    path: 'users',
                    lazy: async () => ({
                      Component: (await import('@/features/users/UsersPage')).UsersPage,
                    }),
                  },
                ],
              },
              {
                path: 'products',
                lazy: async () => ({
                  Component: (await import('@/features/catalog/ProductsPage')).ProductsPage,
                }),
              },
              {
                path: 'products/:id',
                lazy: async () => ({
                  Component: (await import('@/features/catalog/ProductDetailPage'))
                    .ProductDetailPage,
                }),
              },
              {
                element: <RequirePermission permission={Permissions.CanManageCatalog} />,
                children: [
                  {
                    path: 'categories',
                    lazy: async () => ({
                      Component: (await import('@/features/catalog/CategoriesPage')).CategoriesPage,
                    }),
                  },
                ],
              },
              {
                path: 'locations',
                lazy: async () => ({
                  Component: (await import('@/features/locations/LocationsPage')).LocationsPage,
                }),
              },
              {
                path: 'locations/:id',
                lazy: async () => ({
                  Component: (await import('@/features/locations/LocationDetailPage'))
                    .LocationDetailPage,
                }),
              },
              {
                path: 'dev/scan',
                // Lazy: the barcode reader is large and only needed on this page.
                lazy: async () => ({
                  Component: (await import('@/features/scan/DevScanPage')).DevScanPage,
                }),
              },
              { path: '*', element: <NotFoundPage /> },
            ],
          },
        ],
      },
    ],
  },
])
