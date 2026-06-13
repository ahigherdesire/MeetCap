export function Logo({ className = 'h-8 w-8' }: { className?: string }) {
  return (
    <span className={`relative inline-flex items-center justify-center ${className}`}>
      <svg viewBox="0 0 32 32" className="h-full w-full" aria-hidden>
        <rect width="32" height="32" rx="9" className="fill-brand-600" />
        <circle cx="16" cy="16" r="6" className="fill-white" />
        <circle cx="16" cy="16" r="3" className="fill-brand-600" />
      </svg>
    </span>
  )
}
