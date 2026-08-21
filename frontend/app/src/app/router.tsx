import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '@/components/ProtectedRoute'
import { AppShell } from '@/components/nav/AppShell'
import { CategoriesPage } from '@/routes/CategoriesPage'
import { EditCategoryPage } from '@/routes/EditCategoryPage'
import { EditExpensePage } from '@/routes/EditExpensePage'
import { ExpenseDetailPage } from '@/routes/ExpenseDetailPage'
import { ExpensesListPage } from '@/routes/ExpensesListPage'
import { HomePage } from '@/routes/HomePage'
import { LoginPage } from '@/routes/LoginPage'
import { NewCategoryPage } from '@/routes/NewCategoryPage'
import { RegisterExpensePage } from '@/routes/RegisterExpensePage'
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
          { path: 'expenses/new', element: <RegisterExpensePage /> },
          { path: 'expenses', element: <ExpensesListPage /> },
          { path: 'expenses/:id', element: <ExpenseDetailPage /> },
          { path: 'expenses/:id/edit', element: <EditExpensePage /> },
          { path: 'categories', element: <CategoriesPage /> },
          { path: 'categories/new', element: <NewCategoryPage /> },
          { path: 'categories/:id/edit', element: <EditCategoryPage /> },
          { path: 'settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
])
