// フロントエンドの層依存チェック（eslint-plugin-boundaries）
//
// .claude/rules/30-frontend.md の依存ルールを機械検知する。
// Django の import-linter、Laravel の deptrac と同じ役割をここで果たす。
import js from '@eslint/js';
import boundaries from 'eslint-plugin-boundaries';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['src/**/*.{ts,tsx}'],
    plugins: { boundaries },
    settings: {
      'boundaries/elements': [
        { type: 'app', pattern: 'src/app/**' },
        // feature 名を capture して、feature 同士の相互参照を判定する
        { type: 'feature', pattern: 'src/features/*/**', capture: ['featureName'] },
        { type: 'shared', pattern: 'src/shared/**' },
        { type: 'config', pattern: 'src/config/**' },
      ],
    },
    rules: {
      'boundaries/element-types': [
        'error',
        {
          default: 'disallow',
          rules: [
            // app は組立点。すべてを参照してよい
            { from: 'app', allow: ['feature', 'shared', 'config'] },
            // feature は shared/config と「自分自身」のみ。
            // 他 feature の内部を触りたくなったら shared へ引き上げる。
            {
              from: 'feature',
              allow: [['feature', { featureName: '${from.featureName}' }], 'shared', 'config'],
            },
            // shared が feature を参照したら、それはもう共有物ではない
            { from: 'shared', allow: ['shared', 'config'] },
            // config は末端。何も参照しない
            { from: 'config', allow: [] },
          ],
        },
      ],
      // 層を跨ぐ相対パスを禁止し、エイリアス（@/...）へ寄せる。
      // '../../../shared/...' は、どの層から来たのかが読めない。
      'no-restricted-imports': [
        'error',
        { patterns: [{ group: ['../../*'], message: '層を跨ぐ相対 import は禁止。@/ エイリアスを使うこと' }] },
      ],
    },
  },
  {
    // 環境変数を読んでよいのは config/ だけ
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/config/**'],
    rules: {
      'no-restricted-properties': [
        'error',
        {
          object: 'import',
          property: 'meta',
          message: '環境変数は src/config/env.ts 経由で読むこと',
        },
      ],
    },
  },
);
