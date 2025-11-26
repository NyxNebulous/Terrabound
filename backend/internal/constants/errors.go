package constants

import (
	"github.com/heroiclabs/nakama-common/runtime"
)

const (
	CodeCanceled           = 1
	CodeUnknown            = 2
	CodeInvalidArgument    = 3
	CodeDeadlineExceeded   = 4
	CodeNotFound           = 5
	CodeAlreadyExists      = 6
	CodePermissionDenied   = 7
	CodeResourceExhausted  = 8
	CodeFailedPrecondition = 9
	CodeAborted            = 10
	CodeOutOfRange         = 11
	CodeUnimplemented      = 12
	CodeInternal           = 13
	CodeUnavailable        = 14
	CodeDataLoss           = 15
	CodeUnauthenticated    = 16
)

var (
	ErrCanceled           = runtime.NewError("operation was cancelled by the client", CodeCanceled)
	ErrUnknown            = runtime.NewError("an unknown server error occurred", CodeUnknown)
	ErrBadInput           = runtime.NewError("input payload or parameters contained invalid data", CodeInvalidArgument)
	ErrTimeout            = runtime.NewError("the operation did not complete within the allotted time", CodeDeadlineExceeded)
	ErrNotFound           = runtime.NewError("the requested resource could not be found", CodeNotFound)
	ErrAlreadyExists      = runtime.NewError("the resource being created already exists", CodeAlreadyExists)
	ErrPermissionDenied   = runtime.NewError("the user is not authorized to perform this operation", CodePermissionDenied)
	ErrResourceExhausted  = runtime.NewError("the server is out of a necessary resource (e.g., storage limits)", CodeResourceExhausted)
	ErrFailedPrecondition = runtime.NewError("operation was rejected as the system is not in a required state", CodeFailedPrecondition)
	ErrAborted            = runtime.NewError("the operation was aborted due to a conflict or retryable error", CodeAborted)
	ErrOutOfRange         = runtime.NewError("the requested index or range is outside the resource limits", CodeOutOfRange)
	ErrUnimplemented      = runtime.NewError("the requested feature or method is not yet implemented", CodeUnimplemented)
	ErrInternalError      = runtime.NewError("an unexpected error occurred in the server's backend logic", CodeInternal)
	ErrUnavailable        = runtime.NewError("the service is temporarily unavailable", CodeUnavailable)
	ErrDataLoss           = runtime.NewError("unrecoverable data loss or corruption occurred", CodeDataLoss)
	ErrUnauthenticated    = runtime.NewError("the request is missing valid authentication credentials", CodeUnauthenticated)
)

// Custom errors
var (
	ErrUnmarshalRequest = runtime.NewError("failed to parse the client's request payload", CodeInvalidArgument)
	ErrMissingParameter = runtime.NewError("a required parameter was missing from the request", CodeInvalidArgument)

	ErrMarshalResponse    = runtime.NewError("failed to serialize the server's response payload", CodeInternal)
	ErrStorageReadFailed  = runtime.NewError("failed to retrieve data from Nakama storage", CodeInternal)
	ErrStorageWriteFailed = runtime.NewError("failed to persist data to Nakama storage", CodeInternal)
	ErrDBOperationFailed  = runtime.NewError("a database transaction or query failed", CodeInternal)

	ErrUserMissing = runtime.NewError("user context missing or unauthenticated", CodeUnauthenticated)
	ErrNotAllowed  = runtime.NewError("operation not allowed for the current user", CodePermissionDenied)

	ErrStateMismatch        = runtime.NewError("security validation failed: OAuth state mismatch", CodePermissionDenied)
	ErrTokenExchangeFailed  = runtime.NewError("failed to exchange authorization code with external provider", CodeInternal)
	ErrExternalAPIError     = runtime.NewError("external API call failed or returned an invalid response", CodeInternal)
	ErrUnmarshalExternalAPI = runtime.NewError("failed to parse response from external API", CodeInternal)
)
