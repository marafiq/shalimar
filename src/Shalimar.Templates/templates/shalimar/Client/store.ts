import { Store } from '@tanstack/store'

export const counterStore = new Store({ count: 0 })

export function incrementCounter() {
    counterStore.setState((s) => ({ ...s, count: s.count + 1 }))
}

