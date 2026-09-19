// Every version script is a step in a release, run either by hand or by the publish workflow. A
// one-line failure is what either wants to read, so the CLIs report the message and exit 1 rather
// than letting a stack trace stand in for it.
export function run(main) {
  try {
    main()
  } catch (error) {
    console.error(error instanceof Error ? error.message : String(error))
    process.exit(1)
  }
}
