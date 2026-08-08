import { defineCollection, z } from 'astro:content';
import { loadDesignDocs, loadTasks } from './lib/content-loader';

// 원본은 tasks/ · GameDesign/design/ 에 그대로 두고, 로더가 읽기만 한다.
// 사이트를 위해 문서를 복사하거나 옮기지 않는다 — 편집 위치가 곧 단일 진실이다.

const design = defineCollection({
  loader: async () =>
    loadDesignDocs().map((d) => ({ id: d.slug, ...d })),
  schema: z.object({
    slug: z.string(),
    relPath: z.string(),
    title: z.string(),
    section: z.string(),
    updated: z.string().nullable(),
    isArchive: z.boolean(),
    bodyHtml: z.string(),
  }),
});

const tasks = defineCollection({
  loader: async () => loadTasks().map((t) => ({ id: t.slug, ...t })),
  schema: z.object({
    taskId: z.string(),
    slug: z.string(),
    title: z.string(),
    owner: z.string(),
    status: z.string(),
    priority: z.string(),
    due: z.string(),
    done: z.boolean(),
    bodyHtml: z.string(),
  }),
});

export const collections = { design, tasks };
