import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider, createRouter } from '@tanstack/react-router'
import { routeTree } from '@generated/routeTree.gen'
import { Provider } from '@react-spectrum/s2'
import './styles.css'

// Get context from server-injected globals
declare global {
    interface Window {
        __SHALIMAR_CONTEXT__?: { environment: string }
        __SHALIMAR_VERSION__?: string
        __SHALIMAR_PROPS__?: unknown
    }
}

// Create router instance
const router = createRouter({ routeTree })

// Type registration for router
declare module '@tanstack/react-router' {
    interface Register {
        router: typeof router
    }
}

const rootElement = document.getElementById('root')!
if (!rootElement.innerHTML) {
    const root = createRoot(rootElement)
    root.render(
        <StrictMode>
            <Provider background="base">
                <RouterProvider router={router} />
            </Provider>
        </StrictMode>,
    )
}

