import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { AppShell } from '@/components/nav/AppShell'
import { CategoriesPage } from '@/routes/CategoriesPage'
import { DashboardPage } from '@/routes/DashboardPage'
import { LoginPage } from '@/routes/LoginPage'
import { ReportsPage } from '@/routes/ReportsPage'
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
          { index: true, element: <DashboardPage /> },
          { path: 'transactions', element: <TransactionsListPage /> },
          { path: 'categories', element: <CategoriesPage /> },
          { path: 'reports', element: <ReportsPage /> },
          { path: 'settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
])
