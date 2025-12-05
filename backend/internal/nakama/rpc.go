package nakama

import (
	"net/http"
)

func HiHandler(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	w.Write([]byte(`{"message":"hi from nakama"}`))
}
