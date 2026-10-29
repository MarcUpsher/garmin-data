export function activityTypeLabel(type: string | null): string {
  if (!type) return 'Activity';
  return type.replaceAll('_', ' ').replace(/\b\w/g, letter => letter.toUpperCase());
}
