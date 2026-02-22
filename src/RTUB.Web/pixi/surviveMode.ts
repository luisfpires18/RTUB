/**
 * Survive Mode — PixiJS TypeScript entry point.
 * IIFE bundle exposing window.surviveModeGame.
 */
import { createSurviveApi } from '@scenes/SurviveScene';

window.surviveModeGame = createSurviveApi();
