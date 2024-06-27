export function convertPath(relativePath: string): string {
  const newPath = relativePath.replace(
    "../",
    `/${import.meta.env.VITE_REPOSITORY_NAME}/`
  );
  return newPath;
}
