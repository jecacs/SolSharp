#!/usr/bin/env bash
set -euo pipefail

if (( $# != 4 )); then
  echo "Usage: $0 <template> <package> <package-version> <output>" >&2
  exit 2
fi

template_path="$1"
package_path="$2"
package_version="$3"
output_path="$4"

if [[ ! -f "${template_path}" ]]; then
  echo "Packed lock template not found: ${template_path}" >&2
  exit 1
fi

if [[ ! -f "${package_path}" ]]; then
  echo "Packed SolSharp artifact not found: ${package_path}" >&2
  exit 1
fi

if [[ "${template_path}" -ef "${output_path}" ]]; then
  echo "Packed lock output must not overwrite its tracked template." >&2
  exit 1
fi

output_directory="$(dirname -- "${output_path}")"
if [[ ! -d "${output_directory}" ]]; then
  echo "Packed lock output directory not found: ${output_directory}" >&2
  exit 1
fi

# NuGet's lock-file contentHash is the base64-encoded SHA-512 of the exact nupkg bytes.
package_content_hash="$(openssl dgst -sha512 -binary "${package_path}" | openssl base64 -A)"
temporary_path="$(mktemp "${output_directory}/solsharp-packed-lock.XXXXXX")"
trap 'rm -f -- "${temporary_path}"' EXIT

# Remote package hashes stay immutable in the tracked template. Only the package built by this
# workflow is dynamic, and its three identity fields are replaced before NuGet reads the lock.
jq --exit-status \
  --arg package_version "${package_version}" \
  --arg package_content_hash "${package_content_hash}" '
    ([.dependencies | to_entries[] as $target |
      $target.value | to_entries[] |
      select(.key == "SolSharp") |
      { target: $target.key, value: .value }]) as $entries |
    if .version != 1 or
       ($entries | length) != 1 or
       $entries[0].target != "net10.0" or
       $entries[0].value.type != "Direct" or
       $entries[0].value.requested != "[0.0.0-local, )" or
       $entries[0].value.resolved != "0.0.0-local" or
       $entries[0].value.contentHash != "__SOLSHARP_NUPKG_SHA512__"
    then error("packed lock template has an unexpected SolSharp entry")
    else
      .dependencies["net10.0"].SolSharp.requested = ("[" + $package_version + ", )") |
      .dependencies["net10.0"].SolSharp.resolved = $package_version |
      .dependencies["net10.0"].SolSharp.contentHash = $package_content_hash
    end
  ' "${template_path}" > "${temporary_path}"

mv -- "${temporary_path}" "${output_path}"
trap - EXIT
