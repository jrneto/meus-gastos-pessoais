import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { AppShell } from '@/components/nav/AppShell'
import { CategoriesPage } from '@/routes/CategoriesPage'
import { ExpensesListPage } from '@/routes/ExpensesListPage'
import { HomePage } from '@/routes/HomePage'
import { LoginPage } from '@/routes/LoginPage'
import { ReportsComingSoonPage } from '@/routes/ReportsComingSoonPage'
import { SettingsPage } from '@/routes/SettingsPage'

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
          { path: 'expenses', element: <ExpensesListPage /> },
          { path: 'categories', element: <CategoriesPage /> },
          { path: 'reports', element: <ReportsComingSoonPage /> },
          { path: 'settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
])
