import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { Provider } from '@react-spectrum/s2'
import { routeTree } from '@generated/routeTree.gen'
import './styles.css'
import type { AppContext } from './appContext'

declare global {
    interface Window {
        __SHALIMAR_CONTEXT__?: AppContext
        __SHALIMAR_PROPS__?: unknown
    }
}

const router = createRouter({
    routeTree,
    context: (typeof window !== 'undefined' ? window.__SHALIMAR_CONTEXT__ : undefined) as AppContext,
})

declare module '@tanstack/react-router' {
    interface Register {
        router: typeof router
    }
}

const rootElement = document.getElementById('root')!
if (!rootElement.innerHTML) {
    createRoot(rootElement).render(
        <StrictMode>
            <Provider background="base">
                <RouterProvider router={router} />
            </Provider>
        </StrictMode>,
    )
}

