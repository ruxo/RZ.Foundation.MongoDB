using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using MongoDB.Driver;
using RZ.Foundation.Types;
using static RZ.Foundation.MongoDb.MongoHelper;
using PureAttribute = System.Diagnostics.Contracts.PureAttribute;

// ReSharper disable SuspiciousTypeConversion.Global

namespace RZ.Foundation.MongoDb;

[PublicAPI]
public static class MongoClientExtensions
{
    #region Retrieval

    extension<T>(IMongoCollection<T> collection)
    {
        /// <summary>
        /// Get the first element that satisfies the predicate.
        /// </summary>
        [Pure, PublicAPI]
        public ValueTask<Outcome<T>> Get(Expression<Func<T, bool>> predicate, CancellationToken cancel = default)
            => TryExecute(async () => {
                using var cursor = await collection.FindAsync(predicate, cancellationToken: cancel);
                return await cursor.MoveNextAsync(cancel) ? cursor.Current.First() : FailedOutcome<T>(new(StandardErrorCodes.NotFound));
            });

        [Pure, PublicAPI]
        public ValueTask<Outcome<T>> GetById<TKey>(TKey id, CancellationToken cancel = default)
            => TryExecute(async () => {
                var filter = Builders<T>.Filter.Eq(new StringFieldDefinition<T, TKey>("Id"), id);
                using var cursor = await collection.FindAsync(filter, cancellationToken: cancel);
                return await cursor.MoveNextAsync(cancel) ? cursor.Current.TryFirst().ToOutcome(new(StandardErrorCodes.NotFound)) : FailedOutcome<T>(new(StandardErrorCodes.NotFound));
            });
    }

    extension<T>(IAsyncCursor<T> cursor)
    {
        public ValueTask<Outcome<TResult>> Retrieve<TResult>(Func<IAsyncCursor<T>, ValueTask<Outcome<TResult>>> chain)
            => TryExecute(async () => {
                using var c = cursor;
                return await chain(c);
            });

        [PublicAPI]
        public async ValueTask<Outcome<List<T>>> ExecuteList(CancellationToken cancel = default) {
            try{
                return await cursor.ToListAsync(cancel);
            }
            catch (Exception e){
                return InterpretDatabaseError(e);
            }
        }
    }

    [Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<Outcome<TResult>> Retrieve<T, TResult>(this Task<IAsyncCursor<T>> cursor, Func<IAsyncCursor<T>, ValueTask<Outcome<TResult>>> chain)
        => TryExecute(async () => {
            using var c = await cursor;
            return await chain(c);
        });

    #endregion

    static readonly ReplaceOptions ReplaceUpsertOption = new() { IsUpsert = true };

    static (T, FilterDefinition<T>) GetUpdateCondition<T, TKey>(T data, TimeProvider? clock) where T : IHaveKey<TKey>
        => data is ICanUpdateVersion<T> duv
               ? (GetFinal(duv, clock), Build<T>.Predicate(data.Id, duv.Version))
               : (data, Build<T>.Predicate(data.Id));

    extension<T>(IMongoCollection<T> collection)
    {
        public ValueTask<Outcome<T>> Add(T data, CancellationToken cancel = default)
            => TryExecute(async () => {
                await collection.InsertOneAsync(data, cancellationToken: cancel);
                return data;
            });

        #region Update

        async ValueTask<Outcome<T>> PureUpdate(T data, FilterDefinition<T> predicate, bool upsert, CancellationToken cancel) {
            var option = upsert ? ReplaceUpsertOption : null;
            var result = await collection.ReplaceOneAsync(predicate, data, option, cancel);
            return InterpretUpdateResult(data, result);
        }

        public ValueTask<Outcome<T>> Update(T data,
                                            Expression<Func<T, bool>> predicate,
                                            bool upsert = false,
                                            CancellationToken cancel = default)
            => TryExecute(() => collection.PureUpdate(data, predicate, upsert, cancel));

        public ValueTask<Outcome<T>> Update<TKey>(TKey key, T data, VersionType? current = null,
                                                  bool upsert = false, CancellationToken cancel = default)
            => TryExecute(() => collection.PureUpdate(data,
                                                      current is null ? Build<T>.Predicate(key) : Build<T>.Predicate(key, current.Value),
                                                      upsert, cancel));

        #endregion

        #region Upsert

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<Outcome<T>> Upsert(T data, Expression<Func<T, bool>> predicate, CancellationToken cancel = default)
            => collection.Update(data, predicate, upsert: true, cancel: cancel);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<Outcome<T>> Upsert<TKey>(TKey key, T data, VersionType? current = null,
                                                  CancellationToken cancel = default)
            => collection.Update(key, data, current, upsert: true, cancel: cancel);

        #endregion

        #region Deletion

        public ValueTask<Outcome<Unit>> DeleteAll(Expression<Func<T, bool>> predicate, CancellationToken cancel = default)
            => TryExecute(async () => {
                await collection.DeleteManyAsync(predicate, cancel);
                return unit;
            });

        public ValueTask<Outcome<Unit>> Delete(Expression<Func<T, bool>> predicate, CancellationToken cancel = default)
            => TryExecute(async () => {
                await collection.DeleteOneAsync(predicate, cancel);
                return unit;
            });

        public ValueTask<Outcome<Unit>> Delete<TKey>(TKey key, VersionType? current = null, CancellationToken cancel = default)
            => TryExecute(async () => {
                var filter = current is null ? Build<T>.Predicate(key) : Build<T>.Predicate(key, current.Value);
                await collection.DeleteOneAsync(filter, cancel);
                return unit;
            });

        #endregion
    }

    extension<T, TKey>(IMongoCollection<T> collection) where T : IHaveKey<TKey>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<Outcome<T>> Update(T data, bool upsert = false, TimeProvider? clock = null, CancellationToken cancel = default)
            => TryExecute(() => {
                var (final, predicate) = GetUpdateCondition<T, TKey>(data, clock);
                return collection.PureUpdate(final, predicate, upsert, cancel);
            });

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<Outcome<T>> Upsert(T data, TimeProvider? clock = null, CancellationToken cancel = default)
            => collection.Update<T, TKey>(data, upsert: true, clock, cancel);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<Outcome<Unit>> Delete(T data, CancellationToken cancel = default)
            => collection.Delete(data.Id, (data as IHaveVersion)?.Version, cancel);
    }

    static T GetFinal<T>(ICanUpdateVersion<T> data, TimeProvider? clock)
        => data.WithVersion(clock?.GetUtcNow() ?? DateTimeOffset.UtcNow, data.Version + 1);

    static class Build<T>
    {
        public static FilterDefinition<T> Predicate<TKey>(TKey id)
            => Builders<T>.Filter.Eq(new StringFieldDefinition<T, TKey>("Id"), id);

        public static FilterDefinition<T> Predicate<TKey>(TKey id, VersionType version)
            => Builders<T>.Filter.And(Predicate(id),
                                      Builders<T>.Filter.Eq(new StringFieldDefinition<T, VersionType>(nameof(IHaveVersion.Version)), version));
    }

    static ErrorInfo? InterpretReplaceResult(ReplaceOneResult result) =>
        result.IsAcknowledged
            ? result.UpsertedId is null && result is { ModifiedCount: 0, MatchedCount: 0 }
                  ? new ErrorInfo(StandardErrorCodes.RaceCondition, "Data has changed externally")
                  : null
            : new ErrorInfo(StandardErrorCodes.DatabaseTransactionError, "Failed to update the data", result.ToString());

    static Outcome<T> InterpretUpdateResult<T>(T data, ReplaceOneResult result) {
        var error = InterpretReplaceResult(result);
        return error is null ? data : error;
    }
}