import { Text } from '@react-spectrum/s2'
import { useStore } from '@tanstack/react-store'
import { closeModal, uiStore } from './store'
import { Modal } from './Modal'

export function ModalHost() {
    const { modal } = useStore(uiStore)
    if (!modal) return null

    return (
        <Modal open title={modal.title} onClose={closeModal}>
            <Text>{modal.body}</Text>
        </Modal>
    )
}

