using System;

namespace CsvHelper;

/// <summary>
/// A member of a record whose field does not read, found by <see cref="CsvRecordMapper{T}.FindFailingMembers"/>.
/// </summary>
/// <param name="Member">
/// The member, as error messages name it (<c>Order.Price</c>); for a nested record, the member of the nested type.
/// </param>
/// <param name="FieldIndex">
/// The 0-based index of the member's field, or -1 when it is not known, such as for a nested record none of whose own
/// members fails, or a collection whose check of the whole fails.
/// </param>
/// <param name="Exception">
/// What reading the member threw: a <see cref="FormatException"/> or <see cref="OverflowException"/> for a field that does
/// not parse, a <see cref="CsvValidationException"/>, or the exception of a converter.
/// </param>
public readonly record struct CsvMemberError(string Member, int FieldIndex, Exception Exception);
