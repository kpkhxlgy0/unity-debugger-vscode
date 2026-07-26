export function describeInventoryDifferences(committed, generated) {
  const expected = new Map(
    (committed.assemblies ?? []).map((assembly) => [
      assembly.path,
      assembly.sha256,
    ]),
  );
  const actual = new Map(
    (generated.assemblies ?? []).map((assembly) => [
      assembly.path,
      assembly.sha256,
    ]),
  );
  const paths = [...new Set([...expected.keys(), ...actual.keys()])].sort(
    (left, right) => left.localeCompare(right),
  );

  return paths
    .filter((assemblyPath) => expected.get(assemblyPath) !== actual.get(assemblyPath))
    .map(
      (assemblyPath) =>
        `${assemblyPath}: expected ${expected.get(assemblyPath) ?? "<absent>"}, ` +
        `actual ${actual.get(assemblyPath) ?? "<absent>"}`,
    );
}
