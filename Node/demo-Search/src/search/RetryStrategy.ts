
export interface IRetryStrategy {
    execute<TResult>(predicate: () => TResult): TResult;
}

//In progress
export class AzureSearchRetryStrategy implements IRetryStrategy {
    
    public execute<TResult>(predicate: () => TResult): TResult {

        let shouldRetry: boolean = true;
        let isFirstRetry: boolean = true;
        const exponentialRetryMultiplier: number = 2;
        let nextRetryWaitTime: number = 20;
        const maxRetryCount = 3;
        let currentRetryCount = 0;

        let result: TResult = null;
         
        while(shouldRetry){
            try {
                result = predicate();
                return result;
            } catch (error) {

                //TODO: Log intermediate errors or throw aggregate exception
                //TODO: Allow users to specify a shouldRetry callback to analyze the http status code and rule out non-retryable http status codes.
                let isFirstRetry: boolean = currentRetryCount == 0;
                currentRetryCount++;

                if(isFirstRetry) {
                    continue;
                }

                shouldRetry = currentRetryCount < maxRetryCount;
                if(!shouldRetry) {
                    throw error;
                }
                //sleep nextRetryWaitTime
                nextRetryWaitTime *= exponentialRetryMultiplier;
                
            }
        }
    }   
}