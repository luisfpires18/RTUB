// /api/news calls, input type and limits shared by the news page and its dialogs.
import { type NewsFeed, type NewsPost, outcomeText, call, type Outcome } from './api';

export type Input = { title: string; body: string; publish: boolean; membersOnly: boolean };
export type Tab = 'all' | 'public' | 'private';

export const api = {
  feed: (page: number, tab: Tab = 'all') =>
    call<NewsFeed>('GET', `/api/news?page=${page}${tab === 'all' ? '' : `&visibility=${tab}`}`),
  create: (input: Input) => call<NewsPost>('POST', '/api/news', input),
  update: (id: number, input: Input) => call<NewsPost>('PUT', `/api/news/${id}`, input),
  publish: (id: number) => call<NewsPost>('POST', `/api/news/${id}/publish`),
  unpublish: (id: number) => call<NewsPost>('POST', `/api/news/${id}/unpublish`),
  remove: (id: number) => call<void>('DELETE', `/api/news/${id}`),
};

export const TITLE_MAX = 150;
export const BODY_MAX = 5000;

export const problem = (o: Outcome<unknown>, what: string) =>
  outcomeText(o, what, { forbidden: 'Não tem permissão para gerir as Novidades.', notfound: 'Esta publicação já não existe.' });
