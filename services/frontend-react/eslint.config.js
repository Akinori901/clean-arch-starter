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
      // **この resolver が無いと境界検証が素通りする。**
      // boundaries は import 先のパスを解決してから要素（feature / shared 等）を
      // 判定するため、`@/features/...` のエイリアスを解決できないと
      // 「どの要素か不明」となり、違反を検知できない。
      //
      // 実際に feature 間の相互参照を注入しても落ちなかった。
      // 相対パス（`../../auth/...`）だけが no-restricted-imports で拾われており、
      // **規約が推奨している `@/` 記法のほうが検証されていなかった。**
      //
      // `node: true` も必要。これが無いと相対パスや node_modules の解決を
      // typescript resolver だけに任せることになり、解決に失敗した依存が
      // 「型不明」として素通りしうる。
      'import/resolver': {
        typescript: { project: './tsconfig.json' },
        node: true,
      },
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
