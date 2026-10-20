# Change log

## Unreleased
- A write conflict (Mongo server error `WriteConflict`, code 112) is now `race-condition`
  (`StandardErrorCodes.RaceCondition`), the code a failed version check already returns. It used to be
  `database-transaction`, the same code as a real database failure. A conflict happens in a transaction, when another
  transaction or a write outside any transaction changed the document first, and the driver reports it as a
  `MongoCommandException`.
- Mongo aborts the transaction on a write conflict, so a retry must run the whole transaction again, not only the write.
- Callers that retried on `database-transaction` to catch conflicts can retry on `race-condition` only, and stop
  retrying real failures. Callers that already retry on `race-condition` now retry write conflicts too.
- A REST edge that maps `race-condition` to 409 now answers 409 for a write conflict.
- Unchanged: a duplicate key is `duplication`, any other `MongoException` is `database-transaction`, any other
  exception is not interpreted. The `TransientTransactionError` label is not used, since Mongo also puts it on network
  errors inside a transaction.
- A `MongoWriteException` that carries no write error (only a write concern error) is now `database-transaction`;
  the interpreter used to throw `NullReferenceException` on it.

## 8.0.0
- Change paradigm to Outcome return type