using UnityEngine;

public struct OkPayload<T> { public T Value; public OkPayload(T v) => Value = v; }
public struct ErrPayload<E> { public E Error; public ErrPayload(E e) => Error = e; }

public abstract class ResultBase<E>
{
    protected bool isError = false;
    public bool IsError => isError;
    public bool IsOk => !isError;
    
    protected E error;
    public E Error => error;

    public override string ToString() => isError ? $"Error! {error}." : "Result is OK.";
    public static implicit operator bool(ResultBase<E> res) => res.IsOk;
}

public class Result : ResultBase<string>
{
    protected Result() {}

    public static Result Ok() => new Result();

    public static OkPayload<T> Ok<T>(T value) => new OkPayload<T>(value);
    public static ErrPayload<string> Err(string error) => new ErrPayload<string>(error);
    public static ErrPayload<E> Err<E>(E error) => new ErrPayload<E>(error);

    public static implicit operator Result(ErrPayload<string> payload)
    {
        var res = new Result();
        res.isError = true;
        res.error = payload.Error;
        return res;
    }
}

public class Result<R> : ResultBase<string>
{
    protected R value;
    public R Value => value;

    protected Result() {}

    public static Result<R> Ok(R value)
    {
        var toReturn = new Result<R>();
        toReturn.isError = false;
        toReturn.value = value;
        return toReturn;
    }

    public static Result<R> Err(string errorValue)
    {
        var toReturn = new Result<R>();
        toReturn.isError = true;
        toReturn.error = errorValue;
        return toReturn;
    }

    public static implicit operator Result<R>(OkPayload<R> payload) => Ok(payload.Value);
    public static implicit operator Result<R>(ErrPayload<string> payload) => Err(payload.Error);
}

public class Result<R, E> : ResultBase<E>
{
    protected R value;
    public R Value => value;

    protected Result() {}

    public static Result<R, E> Ok(R value)
    {
        var toReturn = new Result<R, E>();
        toReturn.isError = false;
        toReturn.value = value;
        return toReturn;
    }

    public static Result<R, E> Err(E errorValue)
    {
        var toReturn = new Result<R, E>();
        toReturn.isError = true;
        toReturn.error = errorValue;
        return toReturn;
    }

    public static implicit operator Result<R, E>(OkPayload<R> payload) => Ok(payload.Value);
    public static implicit operator Result<R, E>(ErrPayload<E> payload) => Err(payload.Error);
}