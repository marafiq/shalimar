export type Id = string

export type TaskStatus = 'backlog' | 'todo' | 'in_progress' | 'blocked' | 'done'
export type TaskPriority = 'low' | 'medium' | 'high'

export interface User {
    id: Id
    name: string
    email: string
}

export interface Account {
    id: Id
    name: string
    industry: string
    tier: 'SMB' | 'Mid' | 'Enterprise'
    ownerId: Id
    updatedAt: string
}

export interface Contact {
    id: Id
    accountId: Id
    name: string
    title: string
    email: string
}

export interface Task {
    id: Id
    title: string
    status: TaskStatus
    priority: TaskPriority
    accountId?: Id
    assigneeId?: Id
    dueAt?: string
    tags: string[]
    updatedAt: string
}

export type TaskActor = 'agent' | 'human'

export interface TaskMessage {
    id: Id
    taskId: Id
    ts: string
    actor: TaskActor
    author: string
    body: string
}

export interface ActivityItem {
    id: Id
    ts: string
    kind: 'task' | 'account' | 'system'
    summary: string
}

