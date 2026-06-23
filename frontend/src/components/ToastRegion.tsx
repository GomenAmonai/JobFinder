import { useToasts } from '../hooks/use-toasts';
import { CloseIcon } from './icons';

export function ToastRegion() {
  const { toasts, dismissToast } = useToasts();

  return (
    <div className="toast-region" role="region" aria-label="Notifications" aria-live="polite">
      {toasts.map((toast) => (
        <div key={toast.id} className={`toast toast--${toast.variant}`}>
          <span className="toast__accent" aria-hidden="true" />
          <div className="toast__body">
            <p className="toast__title">{toast.title}</p>
            {toast.description && <p className="toast__description">{toast.description}</p>}
          </div>
          <button
            type="button"
            className="toast__close"
            aria-label="Dismiss notification"
            onClick={() => dismissToast(toast.id)}
          >
            <CloseIcon />
          </button>
        </div>
      ))}
    </div>
  );
}
