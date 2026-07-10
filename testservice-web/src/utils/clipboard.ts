// Copies text to the clipboard, working in both secure (HTTPS / localhost) and
// insecure (plain HTTP) contexts.
//
// The async Clipboard API (navigator.clipboard) is only available in a secure
// context; over plain HTTP it is undefined and throws. Since this app is served
// over HTTP in some environments, we fall back to a temporary-textarea +
// document.execCommand('copy'), which works without a secure context.
//
// Returns true when the copy succeeded, false otherwise — callers can surface feedback.
export async function copyToClipboard(text: string): Promise<boolean> {
  if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // Fall through to the legacy path (insecure context, denied permission, etc.).
    }
  }
  return legacyCopy(text);
}

function legacyCopy(text: string): boolean {
  if (typeof document === 'undefined') {
    return false;
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.setAttribute('readonly', '');
  // Keep it off-screen so focusing/selecting it doesn't scroll or flash the page.
  textarea.style.position = 'fixed';
  textarea.style.top = '-9999px';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);

  // Preserve any existing user selection so we can restore it afterwards.
  const selection = document.getSelection();
  const previousRange =
    selection && selection.rangeCount > 0 ? selection.getRangeAt(0) : null;

  textarea.select();

  let succeeded = false;
  try {
    succeeded = document.execCommand('copy');
  } catch {
    succeeded = false;
  }

  document.body.removeChild(textarea);

  if (previousRange && selection) {
    selection.removeAllRanges();
    selection.addRange(previousRange);
  }

  return succeeded;
}
