import { createContext, useContext, type ReactNode } from 'react'
import type { ShalimarContext, ShalimarProviderProps } from './types'

const ShalimarContextInternal = createContext<ShalimarContext | null>(null)

/**
 * Hook to access the Shalimar context injected by the server
 */
export function useShalimarContext<T extends ShalimarContext = ShalimarContext>(): T {
    const context = useContext(ShalimarContextInternal)
    if (!context) {
        // Try to get from window if not in provider
        if (typeof window !== 'undefined' && window.__SHALIMAR_CONTEXT__) {
            return window.__SHALIMAR_CONTEXT__ as T
        }
        throw new Error('useShalimarContext must be used within ShalimarProvider or context must be injected')
    }
    return context as T
}

/**
 * Provider component for Shalimar context
 */
export function ShalimarProvider({ children, context }: ShalimarProviderProps): ReactNode {
    const value = context ?? (typeof window !== 'undefined' ? window.__SHALIMAR_CONTEXT__ : null)

    if (!value) {
        throw new Error('ShalimarProvider requires context prop or window.__SHALIMAR_CONTEXT__')
    }

    return (
        <ShalimarContextInternal.Provider value={value}>
            {children}
        </ShalimarContextInternal.Provider>
    )
}
