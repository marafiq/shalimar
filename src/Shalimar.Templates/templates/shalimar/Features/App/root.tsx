import { Outlet, createRootRouteWithContext } from '@tanstack/react-router'
import type { AppContext } from './appContext'

export const Route = createRootRouteWithContext<AppContext>()({
    component: () => <Outlet />,
})

