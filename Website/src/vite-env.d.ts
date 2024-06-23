/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_REPOSITORY_NAME: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
