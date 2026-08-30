import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { AppShell } from '@/components/nav/AppShell'
import { CategoriesPage } from '@/routes/CategoriesPage'
import { HomePage } from '@/routes/HomePage'
import { LoginPage } from '@/routes/LoginPage'
import { ReportsComingSoonPage } from '@/routes/ReportsComingSoonPage'
import { SettingsPage } from '@/routes/SettingsPage'
import { TransactionsListPage } from '@/routes/TransactionsListPage'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    path: '/',
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <HomePage /> },
          { path: 'transactions', element: <TransactionsListPage /> },
          { path: 'categories', element: <CategoriesPage /> },
          { path: 'reports', element: <ReportsComingSoonPage /> },
          { path: 'settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
])
