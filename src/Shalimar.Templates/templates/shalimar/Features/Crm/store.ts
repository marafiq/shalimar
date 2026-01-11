import { Store } from '@tanstack/store'
import type { Account, ActivityItem, Contact, Id, Task, TaskActor, TaskMessage, TaskStatus, User } from './types'
import * as api from './mockApi'

export interface CrmState {
    users: User[]
    accounts: Record<Id, Account>
    contactsByAccount: Record<Id, Contact[]>
    tasks: Record<Id, Task>
    messagesByTask: Record<Id, TaskMessage[]>
    activity: ActivityItem[]
    loaded: {
        users: boolean
        accounts: boolean
        tasks: boolean
    }
}

export const crmStore = new Store<CrmState>({
    users: [],
    accounts: {},
    contactsByAccount: {},
    tasks: {},
    messagesByTask: {},
    activity: [],
    loaded: { users: false, accounts: false, tasks: false },
})

export async function ensureUsers() {
    if (crmStore.state.loaded.users) return
    const users = await api.getUsers()
    crmStore.setState((s) => ({ ...s, users, loaded: { ...s.loaded, users: true } }))
}

export async function ensureAccounts() {
    if (crmStore.state.loaded.accounts) return
    const accounts = await api.listAccounts()
    const map = Object.fromEntries(accounts.map((a) => [a.id, a] as const))
    crmStore.setState((s) => ({ ...s, accounts: map, loaded: { ...s.loaded, accounts: true } }))
}

export async function ensureTasks() {
    if (crmStore.state.loaded.tasks) return
    const tasks = await api.listTasks()
    const map = Object.fromEntries(tasks.map((t) => [t.id, t] as const))
    crmStore.setState((s) => ({ ...s, tasks: map, loaded: { ...s.loaded, tasks: true } }))
}

export async function loadContacts(accountId: Id) {
    if (crmStore.state.contactsByAccount[accountId]) return
    const contacts = await api.listContactsByAccount(accountId)
    crmStore.setState((s) => ({ ...s, contactsByAccount: { ...s.contactsByAccount, [accountId]: contacts } }))
}

export async function refreshActivity() {
    const items = await api.listActivity()
    crmStore.setState((s) => ({ ...s, activity: items }))
}

export async function createTask(title: string, accountId?: Id) {
    const task = await api.createTask(title, accountId)
    crmStore.setState((s) => ({ ...s, tasks: { ...s.tasks, [task.id]: task } }))
    await refreshActivity()
    return task
}

export async function updateTask(
    taskId: Id,
    patch: Partial<Pick<Task, 'title' | 'priority' | 'assigneeId' | 'accountId' | 'dueAt'>>,
) {
    const updated = await api.updateTask(taskId, patch)
    if (!updated) return
    crmStore.setState((s) => ({ ...s, tasks: { ...s.tasks, [updated.id]: updated } }))
    await refreshActivity()
}

export async function setTaskStatus(taskId: Id, status: TaskStatus) {
    const updated = await api.updateTaskStatus(taskId, status)
    if (!updated) return
    crmStore.setState((s) => ({ ...s, tasks: { ...s.tasks, [updated.id]: updated } }))
    await refreshActivity()
}

export async function loadTaskMessages(taskId: Id) {
    if (crmStore.state.messagesByTask[taskId]) return
    const msgs = await api.listTaskMessages(taskId)
    crmStore.setState((s) => ({ ...s, messagesByTask: { ...s.messagesByTask, [taskId]: msgs } }))
}

export async function postTaskMessage(taskId: Id, actor: TaskActor, body: string) {
    const msg = await api.postTaskMessage(taskId, actor, body)
    crmStore.setState((s) => ({
        ...s,
        messagesByTask: {
            ...s.messagesByTask,
            [taskId]: [...(s.messagesByTask[taskId] ?? []), msg],
        },
    }))
    await refreshActivity()
    return msg
}

