#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

PLAN_FILE="$ROOT_DIR/docs/PLANO-PROJETO.md"
MODEL="gpt-5-codex"
MAX_ITEMS=999
MAX_REVIEW_LOOPS=5
BUILD_FIX_LOOPS=3
DRY_RUN=0
NO_PUSH=0
VERBOSE=0
PHASE_FILTER=""
PUSH_AT_PHASE_END=1

LOG_ROOT="$ROOT_DIR/logs/codex-phases"
PROMPTS_DIR="$ROOT_DIR/prompts"
IMPLEMENT_TEMPLATE="$PROMPTS_DIR/implementacao-item.md"
REVIEW_TEMPLATE="$PROMPTS_DIR/review-item.md"
FIX_TEMPLATE="$PROMPTS_DIR/correcao-review.md"

usage() {
  cat <<'EOF'
Uso: ./codex-phases.sh [opcoes]

Opcoes:
  --plan-file PATH            Arquivo do plano (default: docs/PLANO-PROJETO.md)
  --model MODEL               Modelo do Codex (default: gpt-5-codex)
  --max-items N               Maximo de itens para processar nesta execucao (default: 999)
  --max-review-loops N        Maximo de loops review/correcao por item (default: 5)
  --phase "Fase 4"            Forca processamento apenas da fase indicada
  --dry-run                   Nao altera arquivos, nao comita e nao faz push
  --no-push                   Nao executa git push ao concluir fase
  --verbose                   Log detalhado
  -h, --help                  Mostra ajuda
EOF
}

log() {
  printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"
}

debug() {
  if [[ "$VERBOSE" -eq 1 ]]; then
    log "DEBUG: $*"
  fi
}

require_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "Erro: comando obrigatorio nao encontrado: $1" >&2
    exit 1
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --plan-file)
      PLAN_FILE="$2"
      shift 2
      ;;
    --model)
      MODEL="$2"
      shift 2
      ;;
    --max-items)
      MAX_ITEMS="$2"
      shift 2
      ;;
    --max-review-loops)
      MAX_REVIEW_LOOPS="$2"
      shift 2
      ;;
    --phase)
      PHASE_FILTER="$2"
      shift 2
      ;;
    --dry-run)
      DRY_RUN=1
      shift
      ;;
    --no-push)
      NO_PUSH=1
      shift
      ;;
    --verbose)
      VERBOSE=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Erro: opcao desconhecida: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ "$NO_PUSH" -eq 1 ]]; then
  PUSH_AT_PHASE_END=0
fi

require_cmd git
require_cmd awk
require_cmd sed
require_cmd grep
require_cmd dotnet
require_cmd codex

if [[ ! -f "$PLAN_FILE" ]]; then
  echo "Erro: arquivo do plano nao encontrado: $PLAN_FILE" >&2
  exit 1
fi

if [[ ! -f "$IMPLEMENT_TEMPLATE" || ! -f "$REVIEW_TEMPLATE" || ! -f "$FIX_TEMPLATE" ]]; then
  echo "Erro: templates de prompt ausentes em $PROMPTS_DIR" >&2
  exit 1
fi

mkdir -p "$LOG_ROOT"

if ! git -C "$ROOT_DIR" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "Erro: $ROOT_DIR nao e um repositorio git" >&2
  exit 1
fi

if [[ "$DRY_RUN" -eq 0 ]]; then
  if [[ -n "$(git -C "$ROOT_DIR" status --porcelain)" ]]; then
    echo "Erro: repositorio com alteracoes pendentes antes de iniciar. Limpe o estado e rode novamente." >&2
    exit 1
  fi

  current_branch="$(git -C "$ROOT_DIR" rev-parse --abbrev-ref HEAD)"
  if [[ "$current_branch" != "main" ]]; then
    log "Trocando branch para main (atual: $current_branch)."
    git -C "$ROOT_DIR" checkout main
  fi
fi

get_next_pending() {
  awk -v phase_filter="$PHASE_FILTER" '
    BEGIN { phase = ""; in_phase = 0; }
    /^### Fase / {
      phase = $0;
      in_phase = 1;
      next;
    }
    /^### / {
      in_phase = 0;
      next;
    }
    {
      if (in_phase && $0 ~ /^- \[ \]/) {
        if (phase_filter == "" || index(phase, phase_filter) > 0) {
          print phase "\t" $0;
          exit 0;
        }
      }
    }
  ' "$PLAN_FILE"
}

phase_has_pending() {
  local phase_header="$1"
  awk -v header="$phase_header" '
    BEGIN { in_target = 0; pending = 0; }
    $0 == header { in_target = 1; next; }
    in_target && /^### Fase / { in_target = 0; }
    in_target && /^- \[ \]/ { pending = 1; exit 0; }
    END { if (pending == 1) exit 0; exit 1; }
  ' "$PLAN_FILE"
}

mark_item_done() {
  local phase_header="$1"
  local item_code="$2"
  local tmp_file
  tmp_file="$(mktemp)"

  awk -v header="$phase_header" -v code="$item_code" '
    BEGIN { in_target = 0; done = 0; }
    {
      if ($0 == header) {
        in_target = 1;
        print;
        next;
      }
      if (in_target && /^### Fase /) {
        in_target = 0;
      }
      if (in_target && done == 0 && $0 ~ /^- \[ \]/ && $0 ~ code) {
        sub(/^- \[ \]/, "- [x]");
        done = 1;
      }
      print;
    }
    END { if (done == 0) exit 2; }
  ' "$PLAN_FILE" > "$tmp_file"

  if [[ "$DRY_RUN" -eq 1 ]]; then
    rm -f "$tmp_file"
    log "DRY-RUN: marcaria $item_code como concluido em $PLAN_FILE"
  else
    mv "$tmp_file" "$PLAN_FILE"
  fi
}

mark_phase_done_if_complete() {
  local phase_header="$1"
  local tmp_file
  tmp_file="$(mktemp)"

  if phase_has_pending "$phase_header"; then
    rm -f "$tmp_file"
    return 1
  fi

  awk -v header="$phase_header" '
    BEGIN { updated = 0; }
    {
      if (updated == 0 && $0 == header) {
        if ($0 !~ /✅/) {
          print $0 " ✅";
        } else {
          print $0;
        }
        updated = 1;
        next;
      }
      print;
    }
  ' "$PLAN_FILE" > "$tmp_file"

  if [[ "$DRY_RUN" -eq 1 ]]; then
    rm -f "$tmp_file"
    log "DRY-RUN: marcaria cabecalho da fase como concluido: $phase_header"
  else
    mv "$tmp_file" "$PLAN_FILE"
  fi
  return 0
}

commit_if_changes() {
  local message="$1"
  if [[ "$DRY_RUN" -eq 1 ]]; then
    log "DRY-RUN: commit '$message'"
    return 0
  fi

  git -C "$ROOT_DIR" add -A
  if git -C "$ROOT_DIR" diff --cached --quiet; then
    log "Nenhuma alteracao para commit: $message"
    return 0
  fi

  git -C "$ROOT_DIR" commit -m "$message"
}

run_dotnet_build_or_fix() {
  local item_code="$1"
  local item_text="$2"
  local run_dir="$3"
  local attempt=1
  local build_log="$run_dir/build.log"

  while true; do
    if [[ "$DRY_RUN" -eq 1 ]]; then
      log "DRY-RUN: dotnet build"
      return 0
    fi

    if dotnet build > "$build_log" 2>&1; then
      log "dotnet build OK para $item_code"
      return 0
    fi

    if [[ "$attempt" -gt "$BUILD_FIX_LOOPS" ]]; then
      log "Falha: build nao estabilizou apos $BUILD_FIX_LOOPS tentativas. Veja: $build_log"
      return 1
    fi

    local fix_prompt="$run_dir/build-fix-${attempt}.prompt.md"
    local fix_out="$run_dir/build-fix-${attempt}.out.log"
    local fix_msg="$run_dir/build-fix-${attempt}.last-message.txt"

    cat > "$fix_prompt" <<EOF
Voce implementou o item abaixo e o build falhou.

Item: $item_code - $item_text

Corrija os problemas de compilacao no repositorio.
Restricoes:
- Nao fazer commit nem push.
- Fazer apenas alteracoes necessarias para passar no build.

No final da resposta, em linha isolada, retorne exatamente um dos tokens:
- <fix_status>DONE</fix_status>
- <fix_status>BLOCKED</fix_status>
EOF

    log "Build falhou para $item_code, solicitando correcao automatica (tentativa $attempt/$BUILD_FIX_LOOPS)."
    codex exec \
      --dangerously-bypass-approvals-and-sandbox \
      --cd "$ROOT_DIR" \
      --model "$MODEL" \
      -o "$fix_msg" \
      - < "$fix_prompt" > "$fix_out" 2>&1 || true

    if grep -q "<fix_status>BLOCKED</fix_status>" "$fix_out" || grep -q "<fix_status>BLOCKED</fix_status>" "$fix_msg"; then
      log "Correcao de build bloqueada pelo agente. Veja: $fix_out"
      return 1
    fi

    if ! grep -q "<fix_status>DONE</fix_status>" "$fix_out" && ! grep -q "<fix_status>DONE</fix_status>" "$fix_msg"; then
      log "Token de correcao de build ausente. Veja: $fix_out"
      return 1
    fi

    attempt=$((attempt + 1))
  done
}

render_prompt_with_context() {
  local template="$1"
  local out_file="$2"
  local phase_header="$3"
  local item_code="$4"
  local item_text="$5"

  {
    cat "$template"
    echo
    echo "## Contexto da execucao"
    echo "- Arquivo de plano: $PLAN_FILE"
    echo "- Fase atual: $phase_header"
    echo "- Item alvo: $item_code - $item_text"
    echo "- Repositorio: $ROOT_DIR"
  } > "$out_file"
}

items_done=0

while [[ "$items_done" -lt "$MAX_ITEMS" ]]; do
  next_line="$(get_next_pending || true)"
  if [[ -z "$next_line" ]]; then
    log "Nenhum item pendente encontrado (filtro de fase: '${PHASE_FILTER:-<todos>}')."
    break
  fi

  phase_header="$(printf '%s\n' "$next_line" | awk -F '\t' '{print $1}')"
  item_line="$(printf '%s\n' "$next_line" | awk -F '\t' '{print $2}')"
  item_code="$(printf '%s\n' "$item_line" | sed -n 's/.*\(F[0-9][0-9]\).*/\1/p')"
  item_text="$(printf '%s\n' "$item_line" | sed -E 's/^- \[ \] //')"
  phase_label="$(printf '%s\n' "$phase_header" | sed -n 's/^### \(Fase [0-9][0-9]*\).*/\1/p')"
  phase_slug="$(printf '%s\n' "$phase_label" | tr '[:upper:]' '[:lower:]' | tr ' ' '-' )"

  if [[ -z "$item_code" ]]; then
    echo "Erro: nao foi possivel extrair codigo de item da linha: $item_line" >&2
    exit 1
  fi

  stamp="$(date '+%Y%m%d-%H%M%S')"
  run_dir="$LOG_ROOT/${stamp}-${phase_slug}-${item_code}"
  mkdir -p "$run_dir"

  log "Processando $phase_label :: $item_code"
  debug "Item: $item_text"
  debug "Logs: $run_dir"

  impl_prompt="$run_dir/implementacao.prompt.md"
  review_prompt="$run_dir/review.prompt.md"
  render_prompt_with_context "$IMPLEMENT_TEMPLATE" "$impl_prompt" "$phase_header" "$item_code" "$item_text"
  render_prompt_with_context "$REVIEW_TEMPLATE" "$review_prompt" "$phase_header" "$item_code" "$item_text"

  if [[ "$DRY_RUN" -eq 1 ]]; then
    log "DRY-RUN: executaria implementacao via codex exec para $item_code"
  else
    impl_out="$run_dir/implementacao.out.log"
    impl_msg="$run_dir/implementacao.last-message.txt"

    codex exec \
      --dangerously-bypass-approvals-and-sandbox \
      --cd "$ROOT_DIR" \
      --model "$MODEL" \
      -o "$impl_msg" \
      - < "$impl_prompt" > "$impl_out" 2>&1 || true

    if grep -q "<task_status>BLOCKED</task_status>" "$impl_out" || grep -q "<task_status>BLOCKED</task_status>" "$impl_msg"; then
      log "Implementacao bloqueada pelo agente para $item_code. Veja: $impl_out"
      exit 1
    fi

    if ! grep -q "<task_status>IMPLEMENTED</task_status>" "$impl_out" && ! grep -q "<task_status>IMPLEMENTED</task_status>" "$impl_msg"; then
      log "Token de implementacao nao encontrado para $item_code. Veja: $impl_out"
      exit 1
    fi
  fi

  if ! run_dotnet_build_or_fix "$item_code" "$item_text" "$run_dir"; then
    exit 1
  fi

  commit_if_changes "feat($phase_slug): $item_code - $item_text"
  last_commit_sha=""
  if [[ "$DRY_RUN" -eq 0 ]]; then
    last_commit_sha="$(git -C "$ROOT_DIR" rev-parse HEAD)"
  fi

  review_loop=1
  while [[ "$review_loop" -le "$MAX_REVIEW_LOOPS" ]]; do
    if [[ "$DRY_RUN" -eq 1 ]]; then
      log "DRY-RUN: executaria review loop $review_loop para $item_code"
      break
    fi

    review_out="$run_dir/review-${review_loop}.out.log"
    review_msg="$run_dir/review-${review_loop}.last-message.txt"

    codex exec review \
      --dangerously-bypass-approvals-and-sandbox \
      --model "$MODEL" \
      --commit "$last_commit_sha" \
      - < "$review_prompt" > "$review_out" 2>&1 || true

    cp "$review_out" "$review_msg"

    if grep -q "<review_status>GREEN</review_status>" "$review_out"; then
      log "Review GREEN para $item_code no loop $review_loop."
      break
    fi

    if ! grep -q "<review_status>CHANGES_REQUIRED</review_status>" "$review_out"; then
      log "Token de review ausente/inesperado no loop $review_loop. Veja: $review_out"
      exit 1
    fi

    fix_prompt="$run_dir/fix-${review_loop}.prompt.md"
    fix_out="$run_dir/fix-${review_loop}.out.log"
    fix_msg="$run_dir/fix-${review_loop}.last-message.txt"

    {
      cat "$FIX_TEMPLATE"
      echo
      echo "## Contexto da execucao"
      echo "- Item alvo: $item_code - $item_text"
      echo "- Commit revisado: $last_commit_sha"
      echo
      echo "## Texto do review a corrigir"
      cat "$review_out"
    } > "$fix_prompt"

    codex exec \
      --dangerously-bypass-approvals-and-sandbox \
      --cd "$ROOT_DIR" \
      --model "$MODEL" \
      -o "$fix_msg" \
      - < "$fix_prompt" > "$fix_out" 2>&1 || true

    if grep -q "<fix_status>BLOCKED</fix_status>" "$fix_out" || grep -q "<fix_status>BLOCKED</fix_status>" "$fix_msg"; then
      log "Correcao bloqueada no loop $review_loop para $item_code. Veja: $fix_out"
      exit 1
    fi

    if ! grep -q "<fix_status>DONE</fix_status>" "$fix_out" && ! grep -q "<fix_status>DONE</fix_status>" "$fix_msg"; then
      log "Token de correcao ausente no loop $review_loop para $item_code. Veja: $fix_out"
      exit 1
    fi

    if ! run_dotnet_build_or_fix "$item_code" "$item_text" "$run_dir"; then
      exit 1
    fi

    commit_if_changes "fix($phase_slug): ajustes pos-review $item_code loop-$review_loop"
    last_commit_sha="$(git -C "$ROOT_DIR" rev-parse HEAD)"
    review_loop=$((review_loop + 1))
  done

  if [[ "$review_loop" -gt "$MAX_REVIEW_LOOPS" ]]; then
    log "Maximo de loops de review excedido para $item_code."
    exit 1
  fi

  mark_item_done "$phase_header" "$item_code"
  commit_if_changes "chore(plano): concluir $item_code na ${phase_label}"

  phase_completed=0
  if mark_phase_done_if_complete "$phase_header"; then
    phase_completed=1
    commit_if_changes "chore(plano): concluir ${phase_label}"
  fi

  if [[ "$phase_completed" -eq 1 && "$PUSH_AT_PHASE_END" -eq 1 && "$DRY_RUN" -eq 0 ]]; then
    log "Fase concluida. Executando push para origin/main."
    git -C "$ROOT_DIR" push origin main
  fi

  items_done=$((items_done + 1))
done

log "Execucao finalizada. Itens processados: $items_done"
