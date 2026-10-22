# Integration tests

The default test suite uses an injected WSL adapter and never runs live WSL operations. The local environment's registered distributions are user data, so no integration test creates, stops, exports, moves, imports, converts, or unregisters them.

To run disposable integration scenarios, first provision a trusted test image and a fresh Windows profile or VM. The v1.0 repository does not automatically provision distributions because that requires a source image and may download files. Add integration cases only when the harness can prove exclusive ownership of the created distribution and storage directory.
